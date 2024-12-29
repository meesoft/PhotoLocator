using MapControl;
using Peter;
using PhotoLocator.Gps;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace PhotoLocator
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable, IMainViewModel, IImageZoomPreviewViewModel
    {
        private const int MaxParallelExifToolOperations = 1;

#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        Task? _loadPicturesTask;
        CancellationTokenSource? _loadCancellation;
        CancellationTokenSource? _previewCancellation;
        CancellationTokenSource? _processCancellation;
        FileSystemWatcher? _fileSystemWatcher;
        DispatcherOperation? _fileSystemWatcherUpdate;
        double _loadImagesProgress;
        bool _titleUpdatePending;
        readonly List<(string Path, BitmapSource Picture)> _pictureCache = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainViewModel()
        {
#if DEBUG
            if (_isInDesignMode)
            {
                Items.InsertOrdered(new PictureItemViewModel());
                MapCenter = new Location(53.5, 8.2);
                _previewPictureTitle = "Preview";
            }
#endif
            Settings = new ObservableSettings();
            Items.CollectionChanged += (s, e) => BeginTitleUpdate();
            Application.Current.MainWindow.Closing += HandleMainWindowClosing;
        }     

        public string WindowTitle
        {
            get
            {
                var result = "PhotoLocator";
                if (!string.IsNullOrEmpty(PhotoFolderPath))
                    result += " - " + PhotoFolderPath;
                var checkedCount = Items.Count(p => p.IsChecked);
                if (checkedCount > 0)
                    result += $" - {checkedCount} of {Items.Count} selected";
                else
                    result += $" - {Items.Count} items";
                return result;
            }
        }

        public bool IsProgressBarVisible { get => _isProgressBarVisible; set => SetProperty(ref _isProgressBarVisible, value); }
        private bool _isProgressBarVisible = _isInDesignMode;

        public TaskbarItemProgressState TaskbarProgressState { get => _taskbarProgressState; set => SetProperty(ref _taskbarProgressState, value); }
        TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;

        public double ProgressBarValue { get => _progressBarValue; set => SetProperty(ref _progressBarValue, value); }
        private double _progressBarValue;

        public bool ProgressBarIsIndeterminate { get => _progressBarIsIndeterminate; set => SetProperty(ref _progressBarIsIndeterminate, value); }
        private bool _progressBarIsIndeterminate;

        public string? ProgressBarText { get => _progressBarText; set => SetProperty(ref _progressBarText, value); }
        private string? _progressBarText;

        public bool IsWindowEnabled { get => _isWindowEnabled; set => SetProperty(ref _isWindowEnabled, value); }
        private bool _isWindowEnabled = true;

        public IEnumerable<string> PhotoFileExtensions { get; set; } = [];

        public ObservableSettings Settings { get; }
        ISettings IMainViewModel.Settings => Settings;

        public string? PhotoFolderPath
        {
            get => _isInDesignMode ? nameof(PhotoFolderPath) : _photoFolderPath;
            set => SetFolderPathAsync(value?.TrimPath()).WithExceptionShowing();
        }
        private string? _photoFolderPath;

        public async Task SetFolderPathAsync(string? folderPath, string? selectItemFullPath = null)
        {
            if (!SetProperty(ref _photoFolderPath, folderPath, nameof(PhotoFolderPath)))
                return;
            BeginTitleUpdate();
            await LoadFolderContentsAsync(false, selectItemFullPath);
        }

        public ObservableCollection<PointItem> Points { get; } = [];
        public ObservableCollection<PointItem> Pushpins { get; } = [];
        public ObservableCollection<GpsTrace> Polylines { get; } = [];
        public static Visibility MapToolsVisibility => Visibility.Visible;

        public Location? MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }
        private Location? _mapCenter;

        public Location? SavedLocation
        {
            get => _savedLocation;
            set
            {
                if (SetProperty(ref _savedLocation, value))
                    UpdatePushpins();
            }
        }
        private Location? _savedLocation;

        public ComboBoxItem? SelectedViewModeItem
        {
            get => _selectedViewModeItem;
            set
            {
                if (SetProperty(ref _selectedViewModeItem, value))
                {
                    NotifyPropertyChanged(nameof(IsMapVisible));
                    NotifyPropertyChanged(nameof(IsPreviewVisible));
                    NotifyPropertyChanged(nameof(InSplitViewMode));
                    if (IsPreviewVisible)
                        UpdatePreviewPictureAsync().WithExceptionLogging();
                    else
                        PreviewPictureSource = null;
                    UpdatePoints();
                    UpdatePushpins();
                }
            }
        }
        private ComboBoxItem? _selectedViewModeItem;

        public ICommand? ViewModeCommand { get; internal set; }

        public bool IsMapVisible => Equals(SelectedViewModeItem?.Tag, ViewMode.Map) || Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public bool IsPreviewVisible => Equals(SelectedViewModeItem?.Tag, ViewMode.Preview) || Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public bool InSplitViewMode => Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public ICommand ToggleZoomCommand => new RelayCommand(o => PreviewZoom = PreviewZoom > 0 ? 0 : 1);
        public ICommand ZoomToFitCommand => new RelayCommand(o => PreviewZoom = 0);
        public ICommand Zoom100Command => new RelayCommand(o => PreviewZoom = 1);
        public ICommand Zoom200Command => new RelayCommand(o => PreviewZoom = 2);
        public ICommand Zoom400Command => new RelayCommand(o => PreviewZoom = 4);
        public ICommand ZoomInCommand => new RelayCommand(o => PreviewZoom = Math.Min(PreviewZoom + 1, 4));
        public ICommand ZoomOutCommand => new RelayCommand(o => PreviewZoom = Math.Max(PreviewZoom - 1, 0));

        public GridLength MapRowHeight { get => _mapRowHeight; set => SetProperty(ref _mapRowHeight, value); }
        private GridLength _mapRowHeight = new(1, GridUnitType.Star);

        public GridLength PreviewRowHeight { get => _previewRowHeight; set => SetProperty(ref _previewRowHeight, value); }
        private GridLength _previewRowHeight = new(0, GridUnitType.Star);

        public BitmapSource? PreviewPictureSource
        {
            get => _previewPictureSource;
            set
            {
                if (SetProperty(ref _previewPictureSource, value))
                    IsCropControlVisible = false;
            }
        }
        private BitmapSource? _previewPictureSource;

        public string? PreviewPictureTitle { get => _previewPictureTitle; set => SetProperty(ref _previewPictureTitle, value); }
        private string? _previewPictureTitle;

        public int PreviewZoom
        {
            get => _previewZoom;
            set
            {
                if (SetProperty(ref _previewZoom, value) && value > 0)
                    IsCropControlVisible = false;
            }
        }
        private int _previewZoom;

        public OrderedCollection Items { get; } = [];

        public PictureItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!SetProperty(ref _selectedItem, value))
                    return;
                if (value?.GeoTag != null)
                    MapCenter = value.GeoTag;
                UpdatePushpins();
                UpdatePoints();
                UpdatePreviewPictureAsync().WithExceptionLogging();
            }
        }
        private PictureItemViewModel? _selectedItem;

        public IEnumerable<PictureItemViewModel> GetSelectedItems(bool filesOnly)
        {
            var items = Items.Where(item => item.IsChecked && (item.IsFile || !filesOnly)).ToArray();
            if (items.Length > 0)
            {
                SelectIfNotNull(items[0]);
                return items;
            }
            return SelectedItem != null && (SelectedItem.IsFile || !filesOnly) ? [SelectedItem] : [];
        }

        public void SelectIfNotNull(PictureItemViewModel? select)
        {
            if (select is null)
                return;
            SelectedItem = select;
            FocusListBoxItem?.Invoke(select);
        }

        public async Task SelectFileAsync(string outFileName)
        {
            for (var i = 0; i < 10; i++)
            {
                var item = Items.FirstOrDefault(x => string.Equals(x.FullPath, outFileName, StringComparison.CurrentCultureIgnoreCase));
                if (item is null)
                {
                    await Task.Delay(100);
                    continue;
                }
                SelectIfNotNull(item);
                break;
            }
        }

        internal void HandleMapItemSelected(object sender, MapItemEventArgs eventArgs)
        {
            if (eventArgs.Item.Content is not PointItem pointItem)
                return;
            var fileItem = Items.FirstOrDefault(p => p.Name == pointItem.Name);
            SelectIfNotNull(fileItem);
        }

        internal void UpdatePoints()
        {
            if (!IsMapVisible)
                return;
            var updatedPoints = Items.Where(item => item.IsChecked && item.GeoTag != null && item != SelectedItem)
                .ToDictionary(p => p.Name);
            for (int i = Points.Count - 1; i >= 0; i--)
                if (!updatedPoints.Remove(Points[i].Name!))
                    Points.RemoveAt(i);
            foreach (var item in updatedPoints.Values)
                Points.Add(new PointItem { Location = item.GeoTag, Name = item.Name });
        }

        private void UpdatePushpins()
        {
            Pushpins.Clear();
            if (!IsMapVisible)
                return;
            foreach (var trace in Polylines.Where(t => t.Locations.Count == 1 && !string.IsNullOrEmpty(t.Name)))
                Pushpins.Add(new PointItem { Location = trace.Locations[0], Name = trace.Name });
            if (SavedLocation != null)
                Pushpins.Add(new PointItem { Location = SavedLocation, Name = "Saved location" });
            if (SelectedItem?.GeoTag != null)
                Pushpins.Add(new PointItem { Location = SelectedItem.GeoTag, Name = SelectedItem.Name });
        }

        private async Task UpdatePreviewPictureAsync()
        {
            if (_previewCancellation is not null)
                await _previewCancellation.CancelAsync();
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            if (SelectedItem is null || !IsPreviewVisible)
            {
                PreviewPictureSource = null;
                return;
            }
            var selected = SelectedItem;
            if (selected.IsDirectory)
            {
                PreviewPictureSource = null;
                PreviewPictureTitle = selected.Name;
                return;
            }
            _previewCancellation = new CancellationTokenSource();
            var ct = _previewCancellation.Token;
            try
            {
                var cached = _pictureCache.Find(item => item.Path == selected.FullPath);
                if (cached.Path is null)
                {
                    while (_pictureCache.Count > 3)
                        _pictureCache.RemoveAt(0);
                    var loaded = await Task.Run(() => selected.LoadPreview(ct), ct);
                    if (loaded is not null)
                        _pictureCache.Add((selected.FullPath, loaded));
                    if (selected != SelectedItem) // If another item was selected while preview was being loaded
                        return;
                    PreviewPictureSource = loaded;
                }
                else
                    PreviewPictureSource = cached.Picture;
                PreviewPictureTitle = selected.Name + (string.IsNullOrEmpty(selected.MetadataString) ? null : " [" + selected.MetadataString + "]");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                PreviewPictureSource = null;
                PreviewPictureTitle = ex.Message;
            }
        }

        public ICommand AutoGeotagCommand => new RelayCommand(async o =>
        {
            await WaitForPicturesLoadedAsync();
            var selectedItems = GetSelectedItems(true).ToArray();
            if (selectedItems.Length == 0)
            {
                SelectCandidatesCommand.Execute(null);
                selectedItems = GetSelectedItems(true).ToArray();
            }
            if (!selectedItems.Any(item => item.TimeStamp.HasValue && item.CanSaveGeoTag))
            {
                MessageBox.Show("No supported pictures with timestamp and missing geotag found in selection", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var autoTagWin = new AutoTagWindow();
            using var registrySettings = new RegistrySettings();
            var autoTagViewModel = new AutoTagViewModel(Items, selectedItems, Polylines,
                () => { autoTagWin.DialogResult = true; },
                registrySettings);
            autoTagWin.Owner = App.Current.MainWindow;
            autoTagWin.DataContext = autoTagViewModel;
            if (autoTagWin.ShowDialog() == true)
            {
                UpdatePushpins();
                UpdatePoints();
                if (SelectedItem?.GeoTag != null)
                    MapCenter = SelectedItem.GeoTag;
                else if (Points.Count > 0)
                    MapCenter = Points[0].Location;
            }
            autoTagWin.DataContext = null;
            UpdatePreviewPictureAsync().WithExceptionLogging();
        });

        public ICommand CopyLocationCommand => new RelayCommand(o =>
        {
            SavedLocation = MapCenter;
        });

        public ICommand PasteLocationCommand => new RelayCommand(o =>
        {
            if (SavedLocation is null)
                throw new UserMessageException("You need to save a location before applying");
            foreach (var item in GetSelectedItems(true).Where(i => i.CanSaveGeoTag && !Equals(i.GeoTag, SavedLocation)))
            {
                item.GeoTag = SavedLocation;
                item.GeoTagSaved = false;
            }
            UpdatePushpins();
            UpdatePoints();
            MapCenter = SavedLocation;
        });

        public async Task RunProcessWithProgressBarAsync(Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel? focusItem = null)
        {
            using var cursor = new MouseCursorOverride();
            ProgressBarIsIndeterminate = false;
            ProgressBarValue = 0;
            TaskbarProgressState = TaskbarItemProgressState.Normal;
            ProgressBarText = text;
            IsProgressBarVisible = true;
            IsWindowEnabled = false;
            _processCancellation = new CancellationTokenSource();
            var ct = _processCancellation.Token;
            try
            {
                await body(progress =>
                {
                    ct.ThrowIfCancellationRequested();
                    ProgressBarIsIndeterminate = progress < 0;
                    ProgressBarValue = Math.Max(ProgressBarValue, progress);
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TaskbarProgressState = TaskbarItemProgressState.Error;
                IsProgressBarVisible = false;
                ExceptionHandler.ShowException(ex);
            }
            finally
            {
                IsWindowEnabled = true;
                IsProgressBarVisible = false;
                TaskbarProgressState = TaskbarItemProgressState.None;
                if (focusItem is not null)
                    SelectIfNotNull(focusItem);
                else if (SelectedItem != null)
                    SelectIfNotNull(SelectedItem);
                await _processCancellation.CancelAsync();
                _processCancellation.Dispose();
                _processCancellation = null;
            }
        }

        private void HandleMainWindowClosing(object? sender, CancelEventArgs e)
        {
            if (IsProgressBarVisible && _processCancellation is not null && !_processCancellation.IsCancellationRequested)
            {
                _processCancellation.Cancel();
                e.Cancel = true;
            }
        }

        public ICommand SaveGeotagsCommand => new RelayCommand(async o =>
        {
            var updatedPictures = Items.Where(i => i.GeoTagUpdated).ToArray();
            if (updatedPictures.Length == 0)
                return;
            PauseFileSystemWatcher();
            await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                int i = 0;
                await Parallel.ForEachAsync(updatedPictures, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelExifToolOperations, CancellationToken = ct }, 
                    async (item, ct) =>
                    {
                        await item.SaveGeoTagAsync(ct);
                        progressCallback((double)Interlocked.Increment(ref i) / updatedPictures.Length);
                    });
                await Task.Delay(10, ct);
            }, "Saving...");
            ResumeFileSystemWatcher();
        });
       
        public ICommand RenameCommand => new RelayCommand(async o =>
        {
            var selectedItems = GetSelectedItems(false).ToArray();
            if (selectedItems.Length == 0)
                return;
            var focused = SelectedItem;
            if (selectedItems.Any(i => i.ThumbnailImage is null))
                await WaitForPicturesLoadedAsync();
            var renameWin = new RenameWindow(selectedItems, Items, focused!, Settings);
            renameWin.Owner = App.Current.MainWindow;
            renameWin.DataContext = renameWin;
            PauseFileSystemWatcher();
            renameWin.ShowDialog();
            ResumeFileSystemWatcher();
            renameWin.DataContext = null;
            _pictureCache.Clear();
            if (focused != null)
                FocusListBoxItem?.Invoke(focused);
            UpdatePreviewPictureAsync().WithExceptionLogging();
            UpdatePushpins();
            UpdatePoints();
        });

        public Func<string?>? GetSelectedMapLayerName { get; internal set; }

        public ICommand SlideShowCommand => new RelayCommand(o =>
        {
            var pictures = Items.Where(item => item.IsFile).ToList();
            if (pictures.Count == 0)
                return;
            var slideShowWin = new SlideShowWindow(pictures, SelectedItem?.IsFile == true ? SelectedItem : Items.First(),
                GetSelectedMapLayerName?.Invoke(), Settings);
            slideShowWin.Owner = App.Current.MainWindow;
            slideShowWin.ShowDialog();
            slideShowWin.Dispose();
            SelectIfNotNull(slideShowWin.SelectedPicture);
        });

        public ICommand SettingsCommand => new RelayCommand(o =>
        {
            var previousPhotoFileExtensions = Settings.PhotoFileExtensions;
            var previousThumbnailSize = Settings.ThumbnailSize;
            var previousScalingMode = Settings.BitmapScalingMode;
            var previousResamplingOptions = Settings.ResamplingOptions;
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = App.Current.MainWindow;
            settingsWin.Settings.AssignSettings(Settings);
            settingsWin.Settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.BitmapScalingMode))
                    Settings.BitmapScalingMode = settingsWin.Settings.BitmapScalingMode;
                else if (e.PropertyName is nameof(Settings.LanczosUpscaling) or nameof(Settings.LanczosDownscaling))
                    Settings.ResamplingOptions = settingsWin.Settings.ResamplingOptions;
            };
            settingsWin.DataContext = settingsWin.Settings;
            if (settingsWin.ShowDialog() == true)
            {
                bool refresh =
                    settingsWin.Settings.PhotoFileExtensions != previousPhotoFileExtensions ||
                    settingsWin.Settings.ThumbnailSize != previousThumbnailSize ||
                    settingsWin.Settings.ShowFolders != Settings.ShowFolders;
                Settings.AssignSettings(settingsWin.Settings);
                PhotoFileExtensions = Settings.CleanPhotoFileExtensions();
                Settings.PhotoFileExtensions = String.Join(", ", PhotoFileExtensions);

                if (refresh)
                    RefreshFolderCommand.Execute(null);
            }
            else
            {
                Settings.BitmapScalingMode = previousScalingMode;
                Settings.ResamplingOptions = previousResamplingOptions;
            }
        });

        public static ICommand AboutCommand => new RelayCommand(o =>
        {
            var aboutWin = new AboutWindow();
            aboutWin.Owner = App.Current.MainWindow;
            aboutWin.ShowDialog();
        });

        public ICommand BrowseForPhotosCommand => new RelayCommand(o =>
        {
            var browser = new System.Windows.Forms.FolderBrowserDialog();
            if (PhotoFolderPath is not null)
                browser.InitialDirectory = PhotoFolderPath;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                PhotoFolderPath = browser.SelectedPath;
        });

        public ICommand RefreshFolderCommand => new RelayCommand(o => LoadFolderContentsAsync(true).WithExceptionLogging());

        public Action<object>? FocusListBoxItem { get; internal set; }

        public void HandleDroppedFiles(string[] droppedEntries)
        {
            var selectWin = new SelectDropActionWindow(droppedEntries, this) { Owner = App.Current.MainWindow };
            selectWin.ShowDialog();
        }

        public ICommand QuickSearchCommand => new RelayCommand(o =>
        {
            var previous = SelectedItem;
            if (TextInputWindow.Show("Enter part of the file name (without wildcards):", query =>
                {
                    PictureItemViewModel? result;
                    if (string.IsNullOrEmpty(query) || 
                        (result = Items.FirstOrDefault(item => item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))) is null)
                        return false;
                    SelectIfNotNull(result);
                    return true;
                }, "Search") is null)
                SelectIfNotNull(previous);
        });

        public ICommand SetFilterCommand => new RelayCommand(o =>
        {
            var filter = TextInputWindow.Show("Items containing the filter text will be listed first.", "Filter", Items.FilterText ?? string.Empty);
            using var cursor = new MouseCursorOverride();
            Items.FilterText = filter;
            SelectIfNotNull(SelectedItem);
        });
        
        public ICommand SelectAllCommand => new RelayCommand(o =>
        {
            foreach (var item in Items)
                item.IsChecked = true;
            UpdatePoints();
        });

        public ICommand SelectCandidatesCommand => new RelayCommand(async o =>
        {
            await WaitForPicturesLoadedAsync();
            foreach (var item in Items)
                item.IsChecked = item.GeoTag is null && item.TimeStamp.HasValue && item.CanSaveGeoTag;
            _ = GetSelectedItems(true).FirstOrDefault();
            UpdatePoints();
        });

        public ICommand DeselectAllCommand => new RelayCommand(o =>
        {
            foreach (var item in Items)
                item.IsChecked = false;
            UpdatePoints();
        });

        private PictureItemViewModel? GetNearestUnchecked(PictureItemViewModel? focusedItem, PictureItemViewModel[] allSelected)
        {
            if (allSelected.Contains(focusedItem))
            {
                var focusedIndex = Items.IndexOf(focusedItem!);
                focusedItem = null;
                for (int i = focusedIndex + 1; i < Items.Count; i++)
                    if (!Items[i].IsChecked)
                        return Items[i];
                for (int i = focusedIndex - 1; i >= 0; i--)
                    if (!Items[i].IsChecked)
                        return Items[i];
            }
            return focusedItem;
        }

        public ICommand DeleteSelectedCommand => new RelayCommand(async o =>
        {
            var focusedItem = SelectedItem;
            var allSelected = GetSelectedItems(false).ToArray();
            if (allSelected.Length == 0)
                return;
            focusedItem = GetNearestUnchecked(focusedItem, allSelected);
            if (MessageBox.Show($"Delete {allSelected.Length} selected item(s)?" +
                (Settings.IncludeSidecarFiles ? "\nSidecar files will be included." : string.Empty),
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            var selectedIndex = Items.IndexOf(SelectedItem!);
            SelectedItem = null;
            PauseFileSystemWatcher();
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    item.Recycle(Settings.IncludeSidecarFiles);
                    Application.Current.Dispatcher.Invoke(() => Items.Remove(item));
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Deleting...", focusedItem);
            ResumeFileSystemWatcher();
        });

        public ICommand CopySelectedCommand => new RelayCommand(async o =>
        {
            var allSelected = GetSelectedItems(false).ToArray();
            if (allSelected.Length == 0)
                return;
            var target = TextInputWindow.Show($"Copy {allSelected.Length} selected item(s).\n\nDestination:",
                text => !string.IsNullOrWhiteSpace(text) && text != PhotoFolderPath && text != ".", "Copy files", PhotoFolderPath);
            if (target is null)
                return;
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                var targetIsDirectory = Directory.Exists(target) || allSelected.Length > 1 || string.IsNullOrEmpty(Path.GetExtension(target)) || target.EndsWith('\\');
                Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
                int i = 0;
                foreach (var item in allSelected)
                {
                    var targetFileName = targetIsDirectory ? Path.Combine(target, item.Name) : target;
                    if (item.IsFile && File.Exists(targetFileName))
                    {
                        var dialogResult = Application.Current.Dispatcher.Invoke(
                            () => MessageBox.Show(targetFileName + " already exists, do you want to overwrite it?", "Copy files", MessageBoxButton.YesNoCancel, MessageBoxImage.Question));
                        switch (dialogResult)
                        {
                            case MessageBoxResult.Yes: break;
                            case MessageBoxResult.No: i++; continue;
                            default: return;
                        }
                    }
                    item.CopyTo(targetFileName);
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Copying...");
        });

        public ICommand MoveSelectedCommand => new RelayCommand(async o =>
        {
            var focusedItem = SelectedItem;
            var allSelected = GetSelectedItems(false).ToArray();
            if (allSelected.Length == 0)
                return;
            focusedItem = GetNearestUnchecked(focusedItem, allSelected);
            var target = TextInputWindow.Show($"Move {allSelected.Length} selected item(s).\n\nDestination:", 
                text => !string.IsNullOrWhiteSpace(text) && text != PhotoFolderPath && text != ".", "Move files", PhotoFolderPath);
            if (target is null)
                return;
            SelectedItem = null;
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
                int i = 0;
                foreach (var item in allSelected)
                {
                    var targetFileName = Path.Combine(target, item.Name);
                    if (item.IsFile && File.Exists(targetFileName))
                    {
                        var dialogResult = Application.Current.Dispatcher.Invoke(
                            () => MessageBox.Show(targetFileName + " already exists, do you want to overwrite it?", "Move files", MessageBoxButton.YesNoCancel, MessageBoxImage.Question));
                        switch (dialogResult)
                        {
                            case MessageBoxResult.Yes: break;
                            case MessageBoxResult.No: i++; continue;
                            default: return;
                        }
                    }
                    item.MoveTo(targetFileName);
                    Application.Current.Dispatcher.Invoke(() => Items.Remove(item));
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Moving...", focusedItem);
        });

        public ICommand CreateFolderCommand => new RelayCommand(o =>
        {
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            var folderName = TextInputWindow.Show("Folder name:", 
                text => !string.IsNullOrWhiteSpace(text) && text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, 
                "Create folder" );
            if (string.IsNullOrEmpty(folderName))
                return;
            Directory.CreateDirectory(Path.Combine(PhotoFolderPath, folderName));
        });

        public ICommand ExecuteSelectedCommand => new RelayCommand(o =>
        {
            if (SelectedItem is null)
                return;
            if (SelectedItem.IsDirectory)
                PhotoFolderPath = SelectedItem.FullPath;
            else
                Process.Start(new ProcessStartInfo(SelectedItem.FullPath) { UseShellExecute = true });
        });

        public ICommand ExploreCommand => new RelayCommand(o =>
        {
            if (SelectedItem is not null)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select, \"{SelectedItem.FullPath}\"") { UseShellExecute = true });
            else if (!string.IsNullOrEmpty(PhotoFolderPath))
                Process.Start(new ProcessStartInfo("explorer.exe", PhotoFolderPath) { UseShellExecute = true });
        });

        public ICommand CopyPathCommand => new RelayCommand(o =>
        {
            if (SelectedItem != null)
                Clipboard.SetText(SelectedItem.FullPath);
        });

        public ICommand FilePropertiesCommand => new RelayCommand(o =>
        {
            if (SelectedItem != null)
                WinAPI.ShowFileProperties(SelectedItem.FullPath);
        });

        public ICommand ShellContextMenuCommand => new RelayCommand(o =>
        {
            var allSelected = GetSelectedItems(false).ToArray();
            if (allSelected.Length == 0)
                return;
            var files = allSelected.Select(f => new FileInfo(f.FullPath)).ToArray();
            var mainWindow = Application.Current.MainWindow;
            var pt = mainWindow.PointToScreen(Mouse.GetPosition(mainWindow));
            var contextMenu = new ShellContextMenu();
            contextMenu.ShowContextMenu(files, new System.Drawing.Point((int)pt.X, (int)pt.Y));
        });

        public ICommand ParentFolderCommand => new RelayCommand(o =>
        {
            var currentPath = PhotoFolderPath?.TrimEnd('\\');
            var parent = Path.GetDirectoryName(currentPath);
            if (parent != null)
            {
                CancelPictureLoading();
                PhotoFolderPath = parent;
                var select = Items.FirstOrDefault(item => item.FullPath.Equals(currentPath, StringComparison.CurrentCultureIgnoreCase));
                SelectIfNotNull(select);
            }
        });

        public ICommand AdjustTimestampsCommand => new RelayCommand(async o =>
        {
            var selectedItems = GetSelectedItems(true).ToArray();
            if (selectedItems.Length == 0)
                return;
            var offset = TextInputWindow.Show("Timestamp offset (format: +/-hh:mm:ss):", 
                text => !string.IsNullOrWhiteSpace(text) && text[0] is '+' or '-', "Adjust timestamps", "+00:00:00");
            if (string.IsNullOrEmpty(offset))
                return;
            await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                int i = 0;
                await Parallel.ForEachAsync(selectedItems, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelExifToolOperations, CancellationToken = ct }, 
                    async (item, ct) =>
                    {
                        await ExifHandler.AdjustTimeStampAsync(item.FullPath, item.GetProcessedFileName(), offset, Settings.ExifToolPath, ct);
                        progressCallback((double)Interlocked.Increment(ref i) / selectedItems.Length);
                    });
                await Task.Delay(10, ct);
            }, "Adjust timestamps");
        });

        public ICommand ShowMetadataCommand => new RelayCommand(o =>
        {
            if (SelectedItem is null || SelectedItem.IsDirectory)
                return;
            var metadataWin = new MetadataWindow();
            using (new MouseCursorOverride())
            {
                metadataWin.Owner = App.Current.MainWindow;
                metadataWin.Title = SelectedItem.Name;
                metadataWin.Metadata = String.Join("\n", ExifHandler.EnumerateMetadata(SelectedItem.FullPath));
            }
            if (string.IsNullOrEmpty(metadataWin.Metadata))
                throw new UserMessageException("Unable to list metadata for file");
            metadataWin.DataContext = metadataWin;
            metadataWin.ShowDialog();
        });

        public ICommand OpenInMapsCommand => new RelayCommand(o =>
        {
            if (SelectedItem?.GeoTag is null)
            {
                MessageBox.Show("Selected file has no map coordinates.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            using var cursor = new MouseCursorOverride();
            var url = "http://maps.google.com/maps?q=" +
                SelectedItem.GeoTag.Latitude.ToString(CultureInfo.InvariantCulture) + "," +
                SelectedItem.GeoTag.Longitude.ToString(CultureInfo.InvariantCulture) + "&t=h";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        });

        public ICropControl? CropControl { get; internal set; }

        public bool IsCropControlVisible { get => _isCropControlVisible; set => SetProperty(ref _isCropControlVisible, value); }
        private bool _isCropControlVisible;

        public ICommand CropCommand => new RelayCommand(async o =>
        {
            if (IsCropControlVisible)
            {
                try
                {
                    if (SelectedItem is null || CropControl is null || o is not true &&
                        MessageBox.Show("Crop to selection?", "Crop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                        return;
                    string sourceFileName, targetFileName;
                    if (JpegTransformations.IsFileTypeSupported(SelectedItem.Name))
                    {
                        sourceFileName = SelectedItem.FullPath;
                        targetFileName = SelectedItem.GetProcessedFileName();
                        SelectedItem.Rotation = Rotation.Rotate0;
                    }
                    else
                    {
                        sourceFileName = targetFileName =  Path.ChangeExtension(SelectedItem.GetProcessedFileName(), "jpg");
                        if (File.Exists(sourceFileName) && MessageBox.Show($"Do you wish to overwrite the file '{Path.GetFileName(sourceFileName)}'?", "Crop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                            return;
                    }
                    await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(async () =>
                    {
                        progressCallback(-1);
                        if (sourceFileName != SelectedItem.FullPath)
                        {
                            using var file = await FileHelpers.OpenFileWithRetryAsync(SelectedItem.FullPath, ct);
                            GeneralFileFormatHandler.SaveToFile(PreviewPictureSource!, sourceFileName, ExifHandler.LoadMetadata(file), Settings.JpegQuality);
                        }
                        JpegTransformations.Crop(sourceFileName, targetFileName, CropControl.CropRectangle);
                    }, ct), "Cropping");
                }
                finally
                {
                    IsCropControlVisible = false;
                }
            }
            else
            {
                IsCropControlVisible = true;
                if (IsCropControlVisible)
                    PreviewZoom = 0;
            }
        }, o => SelectedItem is not null && SelectedItem.IsFile);

        public JpegTransformCommands JpegTransformCommands => new(this);

        public VideoTransformCommands VideoTransformCommands => new(this);

        private async Task LoadFolderContentsAsync(bool keepSelection, string? selectItemFullPath = null)
        {
            using (new MouseCursorOverride())
            {
                DisposeFileSystemWatcher();
                var selectedName = SelectedItem?.Name;
                CancelPictureLoading();
                if (string.IsNullOrEmpty(PhotoFolderPath))
                    return;
                Items.Clear();
                Polylines.Clear();
                SetupFileSystemWatcher();
                if (Settings.ShowFolders)
                    foreach (var dir in Directory.EnumerateDirectories(PhotoFolderPath))
                        Items.InsertOrdered(new PictureItemViewModel(dir, true, HandleFilePropertyChanged, Settings));
                await AppendFilesAsync(Directory.EnumerateFiles(PhotoFolderPath));
                if (Polylines.Count > 0)
                    MapCenter = Polylines[0].Center;
                if (keepSelection && selectedName != null)
                {
                    var previousSelection = Items.FirstOrDefault(item => item.Name == selectedName);
                    if (previousSelection != null)
                        SelectIfNotNull(previousSelection);
                }
                else if (selectItemFullPath is not null)
                {
                    var selectItem = Items.FirstOrDefault(item => item.FullPath == selectItemFullPath);
                    if (selectItem != null)
                        SelectIfNotNull(selectItem);
                }
                if (SelectedItem is null && Items.Count > 0)
                    SelectIfNotNull(Items.FirstOrDefault(item => item.IsFile) ?? Items[0]);
            }
            await LoadPicturesAsync();
        }       

        private void SetupFileSystemWatcher()
        {
            _fileSystemWatcher = new FileSystemWatcher(PhotoFolderPath!);
            _fileSystemWatcher.Changed += HandleFileSystemWatcherChange;
            _fileSystemWatcher.Created += HandleFileSystemWatcherChange;
            _fileSystemWatcher.Deleted += HandleFileSystemWatcherChange;
            _fileSystemWatcher.Renamed += HandleFileSystemWatcherRename;
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void DisposeFileSystemWatcher()
        {
            _fileSystemWatcher?.Dispose();
            _fileSystemWatcher = null;
        }

        /// <summary> Note that events during pause will be lost </summary>
        public void PauseFileSystemWatcher()
        {
            if (_fileSystemWatcher is not null)
                _fileSystemWatcher.EnableRaisingEvents = false;
        }

        public void ResumeFileSystemWatcher()
        {
            if (_fileSystemWatcher is not null)
                _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void HandleFileSystemWatcherRename(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var renamed = Items.FirstOrDefault(item => item.FullPath == e.OldFullPath);
                if (renamed != null)
                {
                    _pictureCache.Clear();
                    renamed.Renamed(e.FullPath);
                }
                else
                    HandleFileSystemWatcherChange(this, e);
            });
        }

        public async Task WaitForFileSystemWatcherOperation()
        {
            if (_fileSystemWatcherUpdate is null)
                return;
            await _fileSystemWatcherUpdate;
            if (_fileSystemWatcherUpdate.Result is Task operationTask)
                await operationTask;
        }

        private void HandleFileSystemWatcherChange(object sender, FileSystemEventArgs e)
        {
            _fileSystemWatcherUpdate = Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    var removed = Items.FirstOrDefault(item => item.FullPath == e.FullPath);
                    if (removed is null)
                        return;
                    Items.Remove(removed);
                    _pictureCache.RemoveAll(item => item.Path == removed.FullPath);
                }
                else if (e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Renamed)
                {
                    await Task.Delay(1000);
                    var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                    if (File.Exists(e.FullPath) && PhotoFileExtensions.Contains(ext))
                    {
                        var newItem = new PictureItemViewModel(e.FullPath, false, HandleFilePropertyChanged, Settings);
                        if (!Items.InsertOrdered(newItem))
                            return;
                    }
                    else if (Directory.Exists(e.FullPath))
                    {
                        var newItem = new PictureItemViewModel(e.FullPath, true, HandleFilePropertyChanged, Settings);
                        if (!Items.InsertOrdered(newItem))
                            return;
                    }
                    else
                        return;
                    if (e.ChangeType == WatcherChangeTypes.Renamed)
                        _pictureCache.Clear();
                    if (_loadPicturesTask != null)
                        _loadPicturesTask.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(LoadPicturesAsync), TaskScheduler.Default).WithExceptionLogging();
                    else
                        LoadPicturesAsync().WithExceptionLogging();
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    await Task.Delay(1000);
                    var changed = Items.FirstOrDefault(item => item.FullPath == e.FullPath);
                    if (changed == null)
                        return;
                    _pictureCache.RemoveAll(item => item.Path == changed.FullPath);
                    if (changed.IsSelected)
                        UpdatePreviewPictureAsync().WithExceptionLogging();
                    if (changed.ThumbnailImage != null)
                    {
                        Debug.WriteLine("Reloading thumbnail for changed file " + changed.Name);
                        changed.ThumbnailImage = null;
                        if (_loadPicturesTask != null)
                            _loadPicturesTask.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(LoadPicturesAsync), TaskScheduler.Default).WithExceptionLogging();
                        else
                            LoadPicturesAsync().WithExceptionLogging();
                    }
                }
            });
        }

        private void HandleFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PictureItemViewModel.IsChecked))
                BeginTitleUpdate();
        }

        private void BeginTitleUpdate()
        {
            if (_titleUpdatePending)
                return;
            _titleUpdatePending = true;
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                _titleUpdatePending = false;
                NotifyPropertyChanged(nameof(WindowTitle));
            });
        }

        public async Task AppendFilesAsync(IEnumerable<string> fileNames)
        {
            var extensions = PhotoFileExtensions.ToHashSet();
            foreach (var fileName in fileNames)
            {
                if (Items.Any(i => i.FullPath == fileName))
                    continue;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (extensions.Contains(ext))
                    Items.InsertOrdered(new PictureItemViewModel(fileName, false, HandleFilePropertyChanged, Settings));
                else if (ext is ".gpx" or ".kml")
                {
                    var traces = await Task.Run(() => GpsTrace.DecodeGpsTraceFile(fileName, TimeSpan.FromMinutes(1)));
                    foreach (var trace in traces)
                        Polylines.Add(trace);
                }
            }
        }

        public async Task LoadPicturesAsync()
        {
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();

            var candidates = Items.Where(item => item.ThumbnailImage is null).ToArray();
            // Reorder so that we take alternately one from the top and one from the bottom
            var reordered = new PictureItemViewModel[candidates.Length];
            int iStart = 0;
            int iEnd = candidates.Length;
            for (int i = 0; i < candidates.Length; i++)
                reordered[i] = (i & 1) == 0 ? candidates[iStart++] : candidates[--iEnd];
            int progress = 0;
            _loadImagesProgress = 0;
            _loadPicturesTask = Parallel.ForEachAsync(reordered,
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = _loadCancellation.Token },
                async (item, ct) =>
                {
                    await item.LoadThumbnailAndMetadataAsync(ct);
                    if (item.IsSelected && item.GeoTag != null)
                        MapCenter = item.GeoTag;
                    _loadImagesProgress = ++progress / (double)reordered.Length;
                });
            await _loadPicturesTask;
            _loadPicturesTask = null;
        }

        public async Task WaitForPicturesLoadedAsync()
        {
            if (_loadPicturesTask != null)
                await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
                {
                    progressCallback(_loadImagesProgress);
                    while (_loadPicturesTask != null && await Task.WhenAny(_loadPicturesTask, Task.Delay(TimeSpan.FromSeconds(1), ct)) != _loadPicturesTask)
                        progressCallback(_loadImagesProgress);
                    _loadPicturesTask = null;
                }, "Loading images");
        }

        private void CancelPictureLoading()
        {
            _loadCancellation?.Cancel();
            _loadCancellation = null;
            _loadPicturesTask = null;
        }

        public void Dispose()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _previewCancellation?.Cancel();
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            _processCancellation?.Cancel();
            _processCancellation?.Dispose();
            _processCancellation = null;
            DisposeFileSystemWatcher();
        }
    }

    internal enum ViewMode
    {
        Map, Preview, Split
    }
}
