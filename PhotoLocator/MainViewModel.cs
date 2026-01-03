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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace PhotoLocator
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable, IMainViewModel, IImageZoomPreviewViewModel
    {
        private const int MaxParallelExifToolOperations = 1;

        private const string ExifToolNotConfigured = "ExifTool not configured";

#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
#pragma warning disable IDE1006 // Naming Styles
        const bool _isInDesignMode = false;
#pragma warning restore IDE1006 // Naming Styles
#endif

        Task? _loadPicturesTask;
        CancellationTokenSource? _loadCancellation;
        CancellationTokenSource? _previewCancellation;
        CancellationTokenSource? _processCancellation;
        FileSystemWatcher? _fileSystemWatcher;
        double _loadImagesProgress;
        bool _titleUpdatePending;
        bool _loadPicturesPending;
        readonly List<(string Path, BitmapSource Picture)> _pictureCache = [];
        readonly HashSet<string> _gpsTraceFiles = [];

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
            else
#endif
            {
                Application.Current.MainWindow.Closing += HandleMainWindowClosing;
            }
            Settings = new ObservableSettings();
            Items.CollectionChanged += (s, e) => BeginTitleUpdate();
        }     

        public string WindowTitle
        {
            get
            {
                string result;
                if (string.IsNullOrEmpty(PhotoFolderPath))
                    result = "PhotoLocator";
                else
                {
                    var folder = Path.GetFileName(PhotoFolderPath);
                    result = (string.IsNullOrEmpty(folder) ? PhotoFolderPath : folder) + " - PhotoLocator";
                }
                var checkedCount = Items.Count(p => p.IsChecked);
                result += checkedCount > 0 ? $" - {checkedCount} of {Items.Count} selected" : $" - {Items.Count} items";
                return result;
            }
        }

        public bool IsProgressBarVisible { get; set => SetProperty(ref field, value); } = _isInDesignMode;

        public TaskbarItemProgressState TaskbarProgressState { get; set => SetProperty(ref field, value); } = TaskbarItemProgressState.None;

        public double ProgressBarValue { get; set => SetProperty(ref field, value); }

        public bool ProgressBarIsIndeterminate { get; set => SetProperty(ref field, value); }

        public string? ProgressBarText { get; set => SetProperty(ref field, value); }

        public bool IsWindowEnabled { get; set => SetProperty(ref field, value); } = true;

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
            get;
            set => SetProperty(ref field, value);
        }

        public Location? SavedLocation
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    UpdatePushpins();
            }
        }

        public ComboBoxItem? SelectedViewModeItem
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    NotifyPropertyChanged(nameof(IsMapVisible));
                    NotifyPropertyChanged(nameof(IsPreviewVisible));
                    NotifyPropertyChanged(nameof(InSplitViewMode));
                    if (IsPreviewVisible)
                        UpdatePreviewPictureAsync().WithExceptionLogging();
                    else
                        PreviewPictureSource = null;
                    if (!IsMapVisible)
                        IsSunAndMoonVisible = false;
                    UpdatePoints();
                    UpdatePushpins();
                }
            }
        }

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
            get;
            set
            {
                if (SetProperty(ref field, value))
                    IsCropControlVisible = false;
            }
        }

        public string? PreviewPictureTitle { get => _previewPictureTitle; set => SetProperty(ref _previewPictureTitle, value); }
        private string? _previewPictureTitle;

        public int PreviewZoom
        {
            get;
            set
            {
                if (SetProperty(ref field, value) && value > 0)
                    IsCropControlVisible = false;
            }
        }

        public OrderedCollection Items { get; } = [];

        public PictureItemViewModel? SelectedItem
        {
            get;
            set
            {
                if (!SetProperty(ref field, value))
                    return;
                if (value?.GeoTag != null)
                    MapCenter = value.GeoTag;
                UpdatePushpins();
                UpdatePoints();
                UpdatePreviewPictureAsync().WithExceptionLogging();
            }
        }

        public IEnumerable<PictureItemViewModel> GetSelectedItems(bool filesOnly)
        {
            var firstChecked = SelectedItem is not null && SelectedItem.IsChecked 
                && (SelectedItem.IsFile || !filesOnly) 
                ? SelectedItem : null;
            foreach (var item in Items)
                if (item.IsChecked && (item.IsFile || !filesOnly))
                {
                    if (firstChecked is null)
                    {
                        firstChecked = item;
                        SelectIfNotNull(item);
                    }
                    yield return item;
                }
            if (firstChecked is null && SelectedItem != null && (SelectedItem.IsFile || !filesOnly))
                yield return SelectedItem;
        }

        public void SelectIfNotNull(PictureItemViewModel? select)
        {
            if (select is null)
                return;
            SelectedItem = select;
            FocusListBoxItem?.Invoke(select);
        }

        public async Task SelectFileAsync(string fullPath)
        {
            for (var i = 0; i < 15; i++) // We need to wait longer than the delay in the file system watcher
            {
                var item = Items.FirstOrDefault(item => string.Equals(item.FullPath, fullPath, StringComparison.CurrentCultureIgnoreCase));
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
            var fileItem = Items.FirstOrDefault(p => p.Name == eventArgs.Item.Name);
            SelectIfNotNull(fileItem);
        }

        internal void UpdatePoints()
        {
            if (!IsMapVisible)
                return;
            IsSunAndMoonVisible = false;
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

        public async Task UpdatePreviewPictureAsync(string? skipTo = null)
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
                var cached = skipTo is null ? _pictureCache.Find(item => item.Path == selected.FullPath) : default;
                if (cached.Path is null)
                {
                    while (_pictureCache.Count > 3)
                        _pictureCache.RemoveAt(0);
                    var loaded = await Task.Run(() => selected.LoadPreview(ct, skipTo: skipTo), ct);
                    if (loaded is not null && skipTo is null)
                        _pictureCache.Add((selected.FullPath, loaded));
                    if (selected != SelectedItem) // If another item was selected while preview was being loaded
                        return;
                    PreviewPictureSource = loaded;
                }
                else
                    PreviewPictureSource = cached.Picture;
                PreviewPictureTitle = selected.Name + (string.IsNullOrEmpty(selected.MetadataString) ? null : " [" + selected.MetadataString + "]");
            }
            catch (OperationCanceledException) { }
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
            if (autoTagWin.ShowDialog() is true)
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
            if (MapCenter is null)
                return;
            SavedLocation = MapCenter;
            Clipboard.SetText(MapCenter.ToString());
        });

        public ICommand CopyFilesToClipboardCommand => new RelayCommand(o =>
        {
            var collection = new System.Collections.Specialized.StringCollection();
            foreach (var item in GetSelectedItems(false))
                collection.Add(item.FullPath);
            if (collection.Count > 0)
                Clipboard.SetFileDropList(collection);
        });

        public ICommand PasteLocationCommand => new RelayCommand(async o =>
        {
            if (Clipboard.ContainsText())
            {
                try
                {
                    SavedLocation = Location.Parse(Clipboard.GetText());
                }
                catch (Exception ex)
                {
                    throw new UserMessageException("Clipboard does not contain a valid location:\n" + ex.Message, ex);
                }
                foreach (var item in GetSelectedItems(true).Where(i => i.CanSaveGeoTag && !Equals(i.GeoTag, SavedLocation)))
                {
                    item.GeoTag = SavedLocation;
                    item.GeoTagSaved = false;
                }
                UpdatePushpins();
                UpdatePoints();
                MapCenter = SavedLocation;
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList().Cast<string>().ToArray();
                var dropActionWindow = new SelectDropActionWindow(files, this) { Title = "Paste", Owner = App.Current.MainWindow };
                dropActionWindow.ShowDialog();
            }
            else if (Clipboard.ContainsImage() && !string.IsNullOrWhiteSpace(PhotoFolderPath))
            {
                var image = Clipboard.GetImage() ?? throw new UserMessageException("Unable to paste image");
                for (int i = 0; i < 10000; i++)
                {
                    var fileName = Path.Combine(PhotoFolderPath, "ClipboardImage" + i + ".png");
                    if (!File.Exists(fileName))
                    {
                        GeneralFileFormatHandler.SaveToFile(image, fileName);
                        await AddOrUpdateItemAsync(fileName, false, true);
                        break;
                    }
                }
            }
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
            bool completed = false;
            var ct = _processCancellation.Token;
            try
            {
                await body(progress =>
                {
                    ct.ThrowIfCancellationRequested();
                    ProgressBarIsIndeterminate = progress < 0;
                    ProgressBarValue = Math.Max(ProgressBarValue, progress);
                }, ct);
                completed = true;
            }
            finally
            {
                IsWindowEnabled = true;
                IsProgressBarVisible = false;
                TaskbarProgressState = completed ? TaskbarItemProgressState.None : TaskbarItemProgressState.Error;
                if (focusItem is not null)
                    SelectIfNotNull(focusItem);
                else
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
            await using var pause = PauseFileSystemWatcher();
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
        });

        public ICommand PasteMetadataCommand => new RelayCommand(async o =>
        {
            if (SelectedItem is null || SelectedItem.IsDirectory)
                return;
            string? sourceFileName;
            if (Clipboard.ContainsFileDropList())
                sourceFileName = Clipboard.GetFileDropList().Cast<string>().SingleOrDefault();
            else
                sourceFileName = Clipboard.GetText();
            if (!File.Exists(sourceFileName))
                throw new UserMessageException("Clipboard does not contain a valid file name.");
            await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                await ExifTool.TransferMetadataAsync(sourceFileName, SelectedItem.FullPath, SelectedItem.GetProcessedFileName(),
                    Settings.ExifToolPath ?? throw new UserMessageException(ExifToolNotConfigured), ct);
            }, "Pasting metadata...");
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
            await using (PauseFileSystemWatcher())
                renameWin.ShowDialog();
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
            var slideShowItems = Items.Where(item => item.IsFile || item.IsChecked).ToList();
            if (slideShowItems.Count == 0)
            {
                if (SelectedItem is null)
                    return;
                slideShowItems.Add(SelectedItem);
            }
            var slideShowWin = new SlideShowWindow(slideShowItems, SelectedItem?.IsFile is true ? SelectedItem : null,
                GetSelectedMapLayerName?.Invoke(), Settings);
            slideShowWin.Owner = App.Current.MainWindow;
            slideShowWin.ShowDialog();
            slideShowWin.Dispose();
            if (Items.Contains(slideShowWin.SelectedPicture))
                SelectIfNotNull(slideShowWin.SelectedPicture);
        });

        public ICommand SettingsCommand => new RelayCommand(o =>
        {
            var previousPhotoFileExtensions = Settings.PhotoFileExtensions;
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
            if (settingsWin.ShowDialog() is true)
            {
                bool refresh =
                    settingsWin.Settings.PhotoFileExtensions != previousPhotoFileExtensions ||
                    settingsWin.Settings.ThumbnailSize != Settings.ThumbnailSize ||
                    settingsWin.Settings.ShowFolders != Settings.ShowFolders ||
                    settingsWin.Settings.ForceUseExifTool != Settings.ForceUseExifTool;
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

        public bool IsSunAndMoonVisible
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    UpdateSunAndMoonPosition();
            }
        }

        public DateTime SunAndMoonDate
        {
            get;
            set
            {
                if (SetProperty(ref field, value) && IsSunAndMoonVisible)
                    UpdateSunAndMoonPosition();

            }
        } = DateTime.Now;

        private void UpdateSunAndMoonPosition()
        {
            Pushpins.Clear();
            Polylines.Clear();
            Points.Clear();

            if (MapCenter is null || !IsSunAndMoonVisible)
            {
                UpdatePushpins();
                return;
            }

            void AddLineSeg(double azimuth, Color color, string text)
            {
                var pt = CelestialCalculator.GetLocationAtDistance(MapCenter, azimuth, 5);
                var trace = new GpsTrace(new LocationCollection(MapCenter, pt), color);
                Polylines.Add(trace);
                Points.Add(new PointItem { Location = trace.Locations[1], Name = text });
            }

            var now = DateTime.Now.ToUniversalTime();
            var sun = CelestialCalculator.GetSunRiseSet(MapCenter, SunAndMoonDate);
            if (sun.Sunrise.HasValue)
                AddLineSeg(sun.RiseAzimuth!.Value, Colors.Yellow, $"Sunrise: {sun.Sunrise.Value.ToLocalTime()}\nAzimuth: {sun.RiseAzimuth:F0}°");
            if (sun.Sunset.HasValue)
                AddLineSeg(sun.SetAzimuth!.Value, Colors.Yellow, $"Sunset: {sun.Sunset.Value.ToLocalTime()}\nAzimuth: {sun.SetAzimuth:F0}°");
            var sunPos = CelestialCalculator.GetSunPosition(MapCenter, now);
            if (sunPos.HasValue)
                AddLineSeg(sunPos.Value, Colors.Orange, $"Sun now:\nAzimuth: {sunPos:F1}°");

            var moon = CelestialCalculator.GetMoonRiseSet(MapCenter, SunAndMoonDate);
            if (moon.Moonrise.HasValue)
                AddLineSeg(moon.RiseAzimuth!.Value, Colors.LightGray, $"Moonrise: {moon.Moonrise.Value.ToLocalTime()}\n{moon.RiseIllumination * 100:F0}%, azimuth: {moon.RiseAzimuth:F0}°");
            if (moon.Moonset.HasValue)
                AddLineSeg(moon.SetAzimuth!.Value, Colors.LightGray, $"Moonset: {moon.Moonset.Value.ToLocalTime()}\n{moon.SetIllumination * 100:F0}%, azimuth: {moon.SetAzimuth:F0}°");
            var moonPos = CelestialCalculator.GetMoonPosition(MapCenter, now);
            if (moonPos.Azimuth.HasValue)
                AddLineSeg(moonPos.Azimuth.Value, Colors.Gray, $"Moon now: {moonPos.Illumination * 100:F0}%\nAzimuth: {moonPos.Azimuth:F1}°");
        }

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
            var dropActionWindow = new SelectDropActionWindow(droppedEntries, this) { Owner = App.Current.MainWindow };
            dropActionWindow.ShowDialog();
        }

        public ICommand QuickSearchCommand => new RelayCommand(o =>
        {
            var previous = SelectedItem;
            PictureItemViewModel? result = null;
            if (TextInputWindow.Show("Enter part of the file name (without wildcards):", query =>
                {
                    if (string.IsNullOrEmpty(query) || 
                        (result = Items.FirstOrDefault(item => item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))) is null)
                        return false;
                    SelectIfNotNull(result);
                    return true;
                }, "Search") is not null)
                SelectIfNotNull(result);
            else
                SelectIfNotNull(previous);
        });

        public ICommand SetFilterCommand => new RelayCommand(o =>
        {
            var filter = TextInputWindow.Show("Items containing the filter text will be listed first.", query =>
                {
                    if (string.IsNullOrEmpty(query))
                        return true;
                   var firstMatch = Items.FirstOrDefault(item => item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
                   if (firstMatch is null)
                        return false;
                    SelectIfNotNull(firstMatch);
                    return true;
                },  
                "Filter", Items.FilterText);
            if (filter is null)
                return;
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

        public ICommand InvertSelectionCommand => new RelayCommand(o =>
        {
            foreach (var item in Items)
                item.IsChecked = !item.IsChecked;
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
            if (MessageBox.Show(
                (allSelected.Length == 1 ? $"Delete '{allSelected[0].Name}'?" : $"Delete {allSelected.Length} selected items?") +
                (Settings.IncludeSidecarFiles ? "\nSidecar files will be included." : string.Empty),
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            var selectedIndex = Items.IndexOf(SelectedItem!);
            SelectedItem = null;
            await using var pause = PauseFileSystemWatcher();
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    item.Recycle(Settings.IncludeSidecarFiles);
                    Application.Current.Dispatcher.BeginInvoke(() => Items.Remove(item));
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Deleting...", focusedItem);
        });

        public ICommand CopySelectedCommand => new RelayCommand(async o =>
        {
            var allSelected = GetSelectedItems(false).ToArray();
            if (allSelected.Length == 0)
                return;
            var target = TextInputWindow.Show((allSelected.Length == 1 ? $"Copy '{allSelected[0].Name}'?" : $"Copy {allSelected.Length} selected items?") + "\n\nDestination:",
                text => !string.IsNullOrWhiteSpace(text) && text != PhotoFolderPath && text != ".", "Copy files", PhotoFolderPath);
            if (target is null)
                return;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
            var targetIsDirectory = Directory.Exists(target) || allSelected.Length > 1 || string.IsNullOrEmpty(Path.GetExtension(target)) || target.EndsWith('\\');
            if (targetIsDirectory && !ConfirmCreateMissingDirectory(target, "Copy files"))
                return;
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
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
            var target = TextInputWindow.Show((allSelected.Length == 1 ? $"Move '{allSelected[0].Name}'?" : $"Move {allSelected.Length} selected items?") + "\n\nDestination:",
                text => !string.IsNullOrWhiteSpace(text) && text != PhotoFolderPath && text != ".", "Move files", PhotoFolderPath);
            if (target is null)
                return;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
            if (!ConfirmCreateMissingDirectory(target, "Move files"))
                return;
            SelectedItem = null;
            await RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
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

        private static bool ConfirmCreateMissingDirectory(string target, string caption)
        {
            if (Directory.Exists(target))
                return true;
            if (MessageBox.Show($"Target directory '{target}' does not exist. Create it?", caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return false;
            Directory.CreateDirectory(target);
            return true;
        }

        public ICommand CreateFolderCommand => new RelayCommand(async o =>
        {
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            var folderName = TextInputWindow.Show("Folder name:", 
                text => !string.IsNullOrWhiteSpace(text) && text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, 
                "Create folder" );
            if (string.IsNullOrEmpty(folderName))
                return;
            folderName = Path.Combine(PhotoFolderPath, folderName);
            Directory.CreateDirectory(folderName);
            await AddOrUpdateItemAsync(folderName, true, true);
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
                        await ExifTool.AdjustTimestampAsync(item.FullPath, item.GetProcessedFileName(), offset,
                            Settings.ExifToolPath ?? throw new UserMessageException(ExifToolNotConfigured), ct);
                        progressCallback((double)Interlocked.Increment(ref i) / selectedItems.Length);
                    });
                await SelectFileAsync(selectedItems[0].FullPath);
            }, "Adjust timestamps...");
        });

        public ICommand SetTimestampCommand => new RelayCommand(async o =>
        {
            var selectedItems = GetSelectedItems(true).ToArray();
            if (selectedItems.Length == 0)
                return;
            var fileWithTime = selectedItems.FirstOrDefault(item => item.TimeStamp.HasValue);
            var timestamp = TextInputWindow.Show("Timestamp (format: yyyy:mm:dd hh:mm:ss):", 
                text => !string.IsNullOrWhiteSpace(text), "Set timestamp", 
                fileWithTime?.TimeStamp!.Value.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(timestamp))
                return;
            await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                int i = 0;
                await Parallel.ForEachAsync(selectedItems, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelExifToolOperations, CancellationToken = ct },
                    async (item, ct) =>
                    {
                        await ExifTool.SetTimestampAsync(item.FullPath, item.GetProcessedFileName(), timestamp,
                            Settings.ExifToolPath ?? throw new UserMessageException(ExifToolNotConfigured), ct);
                        progressCallback((double)Interlocked.Increment(ref i) / selectedItems.Length);
                    });
                await SelectFileAsync(selectedItems[0].FullPath);
            }, "Set timestamps");
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
                if (Settings.ForceUseExifTool && !string.IsNullOrEmpty(Settings.ExifToolPath))
                    metadataWin.Metadata = String.Join("\n", ExifTool.EnumerateMetadata(SelectedItem.FullPath, Settings.ExifToolPath));
                else
                    metadataWin.Metadata = String.Join("\n", ExifHandler.EnumerateMetadata(SelectedItem.FullPath, Settings.ExifToolPath));
            }
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

        public bool IsCropControlVisible { get; set => SetProperty(ref field, value); }

        public ICommand CropCommand => new RelayCommand(async o =>
        {
            if (IsCropControlVisible)
            {
                if (CropControl is null || PreviewPictureSource is null || o is not true &&
                    MessageBox.Show("Crop to selection?", "Crop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                {
                    IsCropControlVisible = false;
                    return;
                }
                var cropRectangle = CropControl.CropRectangle;
                IsCropControlVisible = false;
                if (SelectedItem is not null && SelectedItem.IsVideo)
                    VideoTransformCommandsShared.CropSelected(cropRectangle);
                else
                    await JpegTransformCommands.CropSelectedItemAsync(PreviewPictureSource, cropRectangle);
            }
            else
            {
                IsCropControlVisible = true;
                if (IsCropControlVisible)
                    PreviewZoom = 0;
            }
        }, o => SelectedItem is not null && SelectedItem.IsFile);

        public double LogViewHeight { get; set => SetProperty(ref field, value); } = 4;

        public ICommand ToggleLogCommand => new RelayCommand(o => LogViewHeight = LogViewHeight > 4 ? 4 : 100);

        public JpegTransformCommands JpegTransformCommands => field ??= new(this);

        public VideoTransformCommands VideoTransformCommands => new(this);

        public VideoTransformCommands VideoTransformCommandsShared => field ??= new(this);

        private async Task LoadFolderContentsAsync(bool keepSelection, string? selectItemFullPath = null)
        {
            using (new MouseCursorOverride())
            {
                DisposeFileSystemWatcher();
                var selectedName = SelectedItem?.Name;
                CancelPictureLoading();
                if (string.IsNullOrEmpty(PhotoFolderPath))
                    return;
                if (!Directory.Exists(PhotoFolderPath))
                    throw new UserMessageException("Folder does not exist.");
                Items.Clear();
                Polylines.Clear();
                _gpsTraceFiles.Clear();
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
        public IAsyncDisposable PauseFileSystemWatcher()
        {
            _fileSystemWatcher?.EnableRaisingEvents = false;
            return new ActionDisposable(async () =>
            {
                _fileSystemWatcher?.EnableRaisingEvents = true;
                if (!string.IsNullOrEmpty(PhotoFolderPath))
                {
                    await AppendFilesAsync(Directory.EnumerateFiles(PhotoFolderPath));
                    await LoadPicturesAsync();
                }
            });
        }

        private void HandleFileSystemWatcherRename(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _pictureCache.Clear();
                var renamed = Items.FirstOrDefault(item => item.FullPath == e.OldFullPath);
                if (renamed != null)
                    renamed.Renamed(e.FullPath);
                else
                    HandleFileSystemWatcherChange(sender, e);
            });
        }

        private void HandleFileSystemWatcherChange(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    var removed = Items.FirstOrDefault(item => item.FullPath == e.FullPath);
                    if (removed is null)
                        return;
                    Items.Remove(removed);
                    _pictureCache.RemoveAll(item => item.Path == removed.FullPath);
                }
                else if (e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Renamed) // Renamed may be forwarded from HandleFileSystemWatcherRename
                {
                    await Task.Delay(1000);
                    if (_fileSystemWatcher != sender)
                        return; // Folder changed, ignore event
                    var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                    if (File.Exists(e.FullPath) && PhotoFileExtensions.Contains(ext))
                    {
                        await AddOrUpdateItemAsync(e.FullPath, false, false);
                    }
                    else if (Directory.Exists(e.FullPath))
                    {
                        await AddOrUpdateItemAsync(e.FullPath, true, false);
                    }
                    else if (GpsTrace.TraceExtensions.Contains(ext))
                    {
                        var traces = await Task.Run(() => GpsTrace.DecodeGpsTraceFile(e.FullPath, TimeSpan.FromMinutes(1)));
                        foreach (var trace in traces)
                            Polylines.Add(trace);
                    }
                    else
                        return;
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
                        Log.Write("Reloading thumbnail for changed file " + changed.Name);
                        changed.ResetThumbnailAndMetadata();
                        await LoadPicturesAsync();
                    }
                }
            });
        }

        public async Task AddOrUpdateItemAsync(string fullPath, bool isDirectory, bool selectItem) 
        {
            var item = Items.InsertOrdered(new PictureItemViewModel(fullPath, isDirectory, HandleFilePropertyChanged, Settings));
            item.ResetThumbnailAndMetadata();
            _pictureCache.RemoveAll(cache => cache.Path == item.FullPath);
            if (item == SelectedItem)
                UpdatePreviewPictureAsync().WithExceptionLogging();
            else if (selectItem)
                SelectIfNotNull(item);
            await LoadPicturesAsync();
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
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (extensions.Contains(ext))
                    Items.InsertOrdered(new PictureItemViewModel(fileName, false, HandleFilePropertyChanged, Settings));
                else if (GpsTrace.TraceExtensions.Contains(ext) && !_gpsTraceFiles.Contains(fileName))
                {
                    _gpsTraceFiles.Add(fileName);
                    var traces = await Task.Run(() => GpsTrace.DecodeGpsTraceFile(fileName, TimeSpan.FromMinutes(1)));
                    foreach (var trace in traces)
                        Polylines.Add(trace);
                }
            }
        }
        
        public async Task LoadPicturesAsync()
        {
            AssertInMainThread();
            if (_loadPicturesTask is not null && !_loadPicturesTask.IsCompleted)
            {
                _loadPicturesPending = true;
                return;
            }

            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var ct = _loadCancellation.Token;
            var sw = Stopwatch.StartNew();
            do
            {
                _loadPicturesPending = false;
                var candidates = Items.Where(item => item.ThumbnailImage is null).ToArray();
                if (candidates.Length == 0)
                    return;
                // Reorder so that we take alternately one from the top and one from the bottom
                var reordered = new PictureItemViewModel[candidates.Length];
                int iStart = 0;
                int iEnd = candidates.Length;
                for (int i = 0; i < candidates.Length; i++)
                    reordered[i] = (i & 1) == 0 ? candidates[iStart++] : candidates[--iEnd];
                int progress = 0;
                _loadImagesProgress = 0;
                _loadPicturesTask = Parallel.ForEachAsync(reordered,
                    new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct },
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
            while (_loadPicturesPending && !ct.IsCancellationRequested);
            Log.Write($"Loaded thumbnails and metadata in {sw.Elapsed.TotalSeconds} s");
        }

        private static void AssertInMainThread()
        {
            Debug.Assert(App.Current.Dispatcher.Thread == Thread.CurrentThread);
        }

        public async Task WaitForPicturesLoadedAsync()
        {
            if (_loadPicturesTask != null)
                await RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
                {
                    progressCallback(_loadImagesProgress);
                    while (_loadPicturesTask != null && await Task.WhenAny(_loadPicturesTask, Task.Delay(TimeSpan.FromSeconds(1), ct)) != _loadPicturesTask)
                        progressCallback(_loadImagesProgress);
                }, "Loading images...");
        }

        private void CancelPictureLoading()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
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
