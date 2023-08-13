﻿using MapControl;
using Microsoft.VisualBasic;
using Peter;
using PhotoLocator.Gps;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Metadata;
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
    internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        Task? _loadPicturesTask;
        CancellationTokenSource? _loadCancellation;
        CancellationTokenSource? _previewCancellation;
        FileSystemWatcher? _fileSystemWatcher;
        bool _titleUpdatePending;
        readonly List <(string Path, BitmapSource Picture)> _pictureCache = new ();

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
                Pictures.Add(new PictureItemViewModel());
                MapCenter = new Location(53.5, 8.2);
                _previewPictureTitle = "Preview";
            }
#endif
            Settings = new ObservableSettings();
            Pictures.CollectionChanged += (s, e) => BeginTitleUpdate();
        }

        public string WindowTitle
        {
            get
            {
                var result = "PhotoLocator";
                if (!string.IsNullOrEmpty(PhotoFolderPath))
                    result += " - " + PhotoFolderPath;
                var checkedCount = Pictures.Count(p => p.IsChecked);
                if (checkedCount > 0)
                    result += $" - {checkedCount} of {Pictures.Count} selected";
                else
                    result += $" - {Pictures.Count} items";
                return result;
            }
        }

        public bool IsProgressBarVisible { get => _isProgressBarVisible; set => SetProperty(ref _isProgressBarVisible, value); }
        private bool _isProgressBarVisible;

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

        public IEnumerable<string> PhotoFileExtensions { get; set; } = Enumerable.Empty<string>();
       
        public ObservableSettings Settings { get; }

        public string? PhotoFolderPath
        {
            get => _isInDesignMode ? nameof(PhotoFolderPath) : _photoFolderPath;
            set
            {
                if (SetProperty(ref _photoFolderPath, value))
                {
                    BeginTitleUpdate();
                    LoadFolderContentsAsync(false).WithExceptionShowing();
                }
            }
        }
        private string? _photoFolderPath;

        public ObservableCollection<PointItem> Points { get; } = new ObservableCollection<PointItem>();
        public ObservableCollection<PointItem> Pushpins { get; } = new ObservableCollection<PointItem>();
        public ObservableCollection<GpsTrace> Polylines { get; } = new ObservableCollection<GpsTrace>();
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

        public ICommand? ToggleZoomCommand => new RelayCommand(o => PreviewZoom = PreviewZoom > 0 ? 0 : 1);
        public ICommand? ZoomToFitCommand => new RelayCommand(o => PreviewZoom = 0);
        public ICommand? Zoom100Command => new RelayCommand(o => PreviewZoom = 1);
        public ICommand? Zoom200Command => new RelayCommand(o => PreviewZoom = 2);
        public ICommand? Zoom400Command => new RelayCommand(o => PreviewZoom = 4);

        public GridLength MapRowHeight { get => _mapRowHeight; set => SetProperty(ref _mapRowHeight, value); }
        private GridLength _mapRowHeight = new(1, GridUnitType.Star);

        public GridLength PreviewRowHeight { get => _previewRowHeight; set => SetProperty(ref _previewRowHeight, value); }
        private GridLength _previewRowHeight = new(0, GridUnitType.Star);

        public BitmapSource? PreviewPictureSource { get => _previewPictureSource; set => SetProperty(ref _previewPictureSource, value); }
        private BitmapSource? _previewPictureSource;

        public string? PreviewPictureTitle { get => _previewPictureTitle; set => SetProperty(ref _previewPictureTitle, value); }
        private string? _previewPictureTitle;

        public int PreviewZoom { get => _previewZoom; set => SetProperty(ref _previewZoom, value); }
        private int _previewZoom;

        public ObservableCollection<PictureItemViewModel> Pictures { get; } = new ObservableCollection<PictureItemViewModel>();

        public PictureItemViewModel? SelectedPicture
        {
            get => _selectedPicture;
            set
            {
                if (SetProperty(ref _selectedPicture, value))
                {
                    if (value?.GeoTag != null)
                        MapCenter = value.GeoTag;
                    UpdatePushpins();
                    UpdatePoints();
                    UpdatePreviewPictureAsync().WithExceptionLogging();
                }
            }
        }
        private PictureItemViewModel? _selectedPicture;

        public IEnumerable<PictureItemViewModel> GetSelectedItems()
        {
            PictureItemViewModel? firstChecked = SelectedPicture != null && SelectedPicture.IsChecked ? SelectedPicture : null;
            foreach (var item in Pictures)
                if (item.IsChecked)
                {
                    if (firstChecked is null)
                    {
                        firstChecked = item;
                        SelectItem(item);
                    }
                    yield return item;
                }
            if (firstChecked is null && SelectedPicture != null)
                yield return SelectedPicture;
        }

        public void SelectItem(PictureItemViewModel select)
        {
            SelectedPicture = select;
            FocusListBoxItem?.Invoke(select);
        }

        public void HandleMapItemSelected(object sender, MapItemEventArgs eventArgs)
        {
            var pointItem = eventArgs.Item.Content as PointItem;
            if (pointItem is null)
                return;
            var fileItem = Pictures.FirstOrDefault(p => p.Name == pointItem.Name);
            if (fileItem is not null)
                SelectItem(fileItem);
        }

        internal void UpdatePoints()
        {
            if (!IsMapVisible)
                return;
            var updatedPoints = Pictures.Where(item => item.IsChecked && item.GeoTag != null && item != SelectedPicture)
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
            if (SelectedPicture?.GeoTag != null)
                Pushpins.Add(new PointItem { Location = SelectedPicture.GeoTag, Name = SelectedPicture.Name });
        }

        private async Task UpdatePreviewPictureAsync()
        {
            _previewCancellation?.Cancel();
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            if (SelectedPicture is null || !IsPreviewVisible)
                return;
            var selected = SelectedPicture;
            if (selected.IsDirectory)
            {
                PreviewPictureSource = null;
                PreviewPictureTitle = selected.Name;
                return;
            }
            _previewCancellation = new CancellationTokenSource();
            var ct = _previewCancellation.Token;
            var textTask = Task.Run(() =>
            {
                var title = selected.Name;
                try
                {
                    var metadata = ExifHandler.GetMetataString(selected.FullPath);
                    if (!string.IsNullOrEmpty(metadata))
                        title += " [" + metadata + "]";
                }
                catch
                {
                }
                ct.ThrowIfCancellationRequested();
                return title;
            }, ct);
            try
            {
                var cached = _pictureCache.Where(item => item.Path == selected.FullPath).FirstOrDefault();
                if (cached.Path is null)
                {
                    while (_pictureCache.Count > 3)
                        _pictureCache.RemoveAt(0);
                    var loaded = await Task.Run(() => selected.LoadPreview(ct), ct);
                    PreviewPictureSource = loaded;
                    if (loaded is not null)
                        _pictureCache.Add((selected.FullPath, loaded));
                }
                else
                    PreviewPictureSource = cached.Picture;
                PreviewPictureTitle = await textTask;
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

        public ICommand AutoTagCommand => new RelayCommand(async o =>
        {
            await WaitForPicturesLoadedAsync();
            var selectedItems = GetSelectedItems().ToArray();
            if (selectedItems.Length == 0)
            {
                SelectCandidatesCommand.Execute(null);
                selectedItems = GetSelectedItems().ToArray();
            }
            if (!selectedItems.Any(item => item.TimeStamp.HasValue && item.CanSaveGeoTag))
            {
                MessageBox.Show("No supported pictures with timestamp and missing geotag found", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var autoTagWin = new AutoTagWindow();
            using var registrySettings = new RegistrySettings();
            var autoTagViewModel = new AutoTagViewModel(Pictures, selectedItems, Polylines,
                () => { autoTagWin.DialogResult = true; }, 
                registrySettings);
            autoTagWin.Owner = App.Current.MainWindow;
            autoTagWin.DataContext = autoTagViewModel;
            PreviewPictureSource = null;
            if (autoTagWin.ShowDialog() == true)
            {
                UpdatePushpins();
                UpdatePoints();
                if (SelectedPicture?.GeoTag != null)
                    MapCenter = SelectedPicture.GeoTag;
                else if (Points.Count > 0)
                    MapCenter = Points[0].Location;
            }
            UpdatePreviewPictureAsync().WithExceptionLogging();
        });

        public ICommand CopyCommand => new RelayCommand(o =>
        {
            SavedLocation = MapCenter;
        });

        public ICommand PasteCommand => new RelayCommand(o =>
        {
            if (SavedLocation is null)
                return;
            foreach (var item in GetSelectedItems().Where(i => i.CanSaveGeoTag && !Equals(i.GeoTag, SavedLocation)))
            {
                item.GeoTag = SavedLocation;
                item.GeoTagSaved = false;
            }
            UpdatePushpins();
            UpdatePoints();
        });

        async Task RunProcessWithProgressBarAsync(Func<Action<double>, Task> body, string text)
        {
            using var cursor = new CursorOverride();
            ProgressBarIsIndeterminate = false;
            ProgressBarValue = 0;
            TaskbarProgressState = TaskbarItemProgressState.Normal;
            ProgressBarText = text;
            IsProgressBarVisible = true;
            IsWindowEnabled = false;
            try
            {
                await body(progress =>
                {
                    if (progress < 0)
                        ProgressBarIsIndeterminate = true;
                    ProgressBarValue = Math.Max(ProgressBarValue, progress);
                });
            }
            catch (Exception ex)
            {
                TaskbarProgressState = TaskbarItemProgressState.Error;
                ExceptionHandler.ShowException(ex);
            }
            finally
            {
                IsWindowEnabled = true;
                IsProgressBarVisible = false;
                TaskbarProgressState = TaskbarItemProgressState.None;
            }
        }

        public ICommand SaveCommand => new RelayCommand(async o =>
        {
            var updatedPictures = Pictures.Where(i => i.GeoTagUpdated).ToArray();
            if (updatedPictures.Length == 0)
                return;
            if (_fileSystemWatcher is not null)
                _fileSystemWatcher.EnableRaisingEvents = false;
            await RunProcessWithProgressBarAsync(async progressCallback =>
            {
                int i = 0;
                await Parallel.ForEachAsync(updatedPictures, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (item, ct) =>
                {
                    await item.SaveGeoTagAsync();
                    progressCallback((double)Interlocked.Increment(ref i) / updatedPictures.Length);
                });
                await Task.Delay(10);
            }, "Saving...");
            if (SelectedPicture != null)
                SelectItem(SelectedPicture);
            if (_fileSystemWatcher is not null)
                _fileSystemWatcher.EnableRaisingEvents = true;
        });

        public ICommand RenameCommand => new RelayCommand(async o =>
        {
            var selectedItems = GetSelectedItems().ToList();
            if (selectedItems.Count == 0)
                return;
            var focused = SelectedPicture;
            if (selectedItems.Any(i => i.ThumbnailImage is null))
                await WaitForPicturesLoadedAsync();
            var renameWin = new RenameWindow(selectedItems, Pictures, Settings);
            renameWin.Owner = App.Current.MainWindow;
            renameWin.DataContext = renameWin;
            PreviewPictureSource = null;
            if (_fileSystemWatcher != null)
                _fileSystemWatcher.EnableRaisingEvents = false;
            renameWin.ShowDialog();
            if (_fileSystemWatcher != null)
                _fileSystemWatcher.EnableRaisingEvents = true;
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
            var pictures = Pictures.Where(item => item.IsFile).ToList();
            if (pictures.Count == 0)
                return;
            var slideShowWin = new SlideShowWindow(pictures, SelectedPicture?.IsFile == true ? SelectedPicture : Pictures.First(),
                GetSelectedMapLayerName?.Invoke(), Settings);
            slideShowWin.Owner = App.Current.MainWindow;
            slideShowWin.ShowDialog();
            SelectItem(slideShowWin.SelectedPicture);
        });

        public ICommand SettingsCommand => new RelayCommand(o =>
        {
            var previousPhotoFileExtensions = Settings.PhotoFileExtensions;
            var previousScalingMode = Settings.BitmapScalingMode;
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = App.Current.MainWindow;
            settingsWin.Settings.AssignSettings(Settings);
            settingsWin.Settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.BitmapScalingMode))
                    Settings.BitmapScalingMode = settingsWin.Settings.BitmapScalingMode;
            };
            settingsWin.DataContext = settingsWin.Settings;
            if (settingsWin.ShowDialog() == true)
            {
                bool refresh =
                    settingsWin.Settings.PhotoFileExtensions != previousPhotoFileExtensions ||
                    settingsWin.Settings.ShowFolders != Settings.ShowFolders;
                Settings.AssignSettings(settingsWin.Settings);
                PhotoFileExtensions = Settings.CleanPhotoFileExtensions();
                Settings.PhotoFileExtensions = String.Join(",", PhotoFileExtensions);

                if (refresh)
                    RefreshFolderCommand.Execute(null);
            }
            else
            {
                Settings.BitmapScalingMode = previousScalingMode;
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
            browser.InitialDirectory = PhotoFolderPath;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                PhotoFolderPath = browser.SelectedPath;
        });

        public ICommand RefreshFolderCommand => new RelayCommand(o => LoadFolderContentsAsync(true).WithExceptionLogging());

        public Action<object>? FocusListBoxItem { get; internal set; }

        public async Task HandleDroppedFilesAsync(string[] droppedEntries)
        {
            if (droppedEntries.All(f => Pictures.Any(i => i.FullPath == f)))
                return;
            await WaitForPicturesLoadedAsync();
            if (droppedEntries.Any(f => Path.GetDirectoryName(f) != PhotoFolderPath))
                PhotoFolderPath = null;
            var fileNames = new List<string>();
            foreach (var path in droppedEntries)
                if (Directory.Exists(path))
                    await AppendFilesAsync(Directory.EnumerateFiles(path));
                else
                    fileNames.Add(path);
            if (fileNames.Count > 0)
            {
                await AppendFilesAsync(fileNames);
                var firstDropped = Pictures.FirstOrDefault(item => item.FullPath == fileNames[0]);
                if (firstDropped != null)
                    SelectItem(firstDropped);
            }
            await LoadPicturesAsync();
        }

        public ICommand QuickSearchCommand => new RelayCommand(o =>
        {
            var query = Interaction.InputBox("Enter part of the file name (without wildcards):", "Search");
            if (string.IsNullOrEmpty(query))
                return;
            var result = Pictures.FirstOrDefault(item => item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
            if (result != null)
                SelectItem(result);
        });

        public ICommand SelectAllCommand => new RelayCommand(o =>
        {
            foreach (var item in Pictures)
                item.IsChecked = true;
            UpdatePoints();
        });

        public ICommand SelectCandidatesCommand => new RelayCommand(async o =>
        {
            await WaitForPicturesLoadedAsync();
            foreach (var item in Pictures)
                item.IsChecked = item.GeoTag is null && item.TimeStamp.HasValue && item.CanSaveGeoTag;
            _ = GetSelectedItems().FirstOrDefault();
            UpdatePoints();
        });

        public ICommand DeselectAllCommand => new RelayCommand(o =>
        {
            foreach (var item in Pictures)
                item.IsChecked = false;
            UpdatePoints();
        });

        private PictureItemViewModel? GetNearestUnchecked(PictureItemViewModel? focusedItem, PictureItemViewModel[] allSelected)
        {
            if (allSelected.Contains(focusedItem))
            {
                var focusedIndex = Pictures.IndexOf(focusedItem!);
                focusedItem = null;
                for (int i = focusedIndex + 1; i < Pictures.Count; i++)
                    if (!Pictures[i].IsChecked)
                        return Pictures[i];
                for (int i = focusedIndex - 1; i >= 0; i--)
                    if (!Pictures[i].IsChecked)
                        return Pictures[i];
            }
            return focusedItem;
        }

        public ICommand DeleteSelectedCommand => new RelayCommand(async o =>
        {
            var focusedItem = SelectedPicture;
            var allSelected = GetSelectedItems().ToArray();
            if (allSelected.Length == 0)
                return;
            focusedItem = GetNearestUnchecked(focusedItem, allSelected);
            if (MessageBox.Show($"Delete {allSelected.Length} selected item(s)?" + 
                (Settings.IncludeSidecarFiles ? "\nSidecar files will be included." : string.Empty), 
                "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            var selectedIndex = Pictures.IndexOf(SelectedPicture!);
            SelectedPicture = null;
            PreviewPictureSource = null;
            PreviewPictureTitle = null;
            await RunProcessWithProgressBarAsync(progressCallback => Task.Run(() =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    item.Recycle(Settings.IncludeSidecarFiles);
                    Application.Current.Dispatcher.Invoke(() => Pictures.Remove(item));
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }), "Deleting...");
            if (focusedItem != null)
                SelectItem(focusedItem);
        });

        public ICommand MoveSelectedCommand => new RelayCommand(o =>
        {
            var focusedItem = SelectedPicture;
            var allSelected = GetSelectedItems().ToArray();
            if (allSelected.Length == 0)
                return;
            focusedItem = GetNearestUnchecked(focusedItem, allSelected);
            var destination = Interaction.InputBox($"Move {allSelected.Length} selected item(s).\n\nDestination:", "Confirm", (PhotoFolderPath ?? string.Empty).Trim('\\'));
            if (string.IsNullOrEmpty(destination) || destination == PhotoFolderPath || destination == ".")
                return;
            using var cursor = new CursorOverride();
            SelectedPicture = null;
            PreviewPictureSource = null;
            PreviewPictureTitle = null;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
            foreach (var item in allSelected)
            {
                item.MoveTo(Path.Combine(destination, item.Name));
                Pictures.Remove(item);
            }
            if (focusedItem != null)
                SelectItem(focusedItem);
        });

        public ICommand CreateFolderCommand => new RelayCommand(o =>
        {
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            var folderName = Interaction.InputBox("Folder name:", "Create folder");
            if (string.IsNullOrEmpty(folderName))
                return;
            Directory.CreateDirectory(Path.Combine(PhotoFolderPath, folderName));
        });

        public ICommand ExecuteSelectedCommand => new RelayCommand(o =>
        {
            if (SelectedPicture is null)
                return;
            if (SelectedPicture.IsDirectory)
                PhotoFolderPath = SelectedPicture.FullPath;
            else
                Process.Start(new ProcessStartInfo(SelectedPicture.FullPath) { UseShellExecute = true });
        });

        public ICommand ExploreCommand => new RelayCommand(o =>
        {
            if (SelectedPicture is not null)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select, \"{SelectedPicture.FullPath}\"") { UseShellExecute = true });
            else if (!string.IsNullOrEmpty(PhotoFolderPath))
                Process.Start(new ProcessStartInfo("explorer.exe", PhotoFolderPath) { UseShellExecute = true });
        });

        public ICommand FilePropertiesCommand => new RelayCommand(o =>
        {
            if (SelectedPicture != null)
                WinAPI.ShowFileProperties(SelectedPicture.FullPath);
        });

        public ICommand ShellContextMenuCommand => new RelayCommand(o =>
        {
            var allSelected = GetSelectedItems().ToArray();
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
                var select = Pictures.FirstOrDefault(item => item.FullPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase));
                if (select != null)
                    SelectItem(select);
            }
        });

        public ICommand ShowMetadataCommand => new RelayCommand(o =>
        {
            if (SelectedPicture is null || SelectedPicture.IsDirectory)
                return;
            var metadataWin = new MetadataWindow();
            using (new CursorOverride())
            {
                metadataWin.Owner = App.Current.MainWindow;
                metadataWin.Title = SelectedPicture.Name;
                metadataWin.Metadata = String.Join("\n", ExifHandler.EnumerateMetadata(SelectedPicture.FullPath));
            }
            if (string.IsNullOrEmpty(metadataWin.Metadata))
                throw new UserMessageException("Unable to list metadata for file");
            metadataWin.DataContext = metadataWin;
            metadataWin.ShowDialog();
        });

        public ICommand OpenInMapsCommand => new RelayCommand(o =>
        {
            if (SelectedPicture?.GeoTag is null)
            {
                MessageBox.Show("Selected file has no map coordinates.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            using var cursor = new CursorOverride();
            var url = "http://maps.google.com/maps?q=" +
                SelectedPicture.GeoTag.Latitude.ToString(CultureInfo.InvariantCulture) + "," +
                SelectedPicture.GeoTag.Longitude.ToString(CultureInfo.InvariantCulture) + "&t=h";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        });

        private async Task LoadFolderContentsAsync(bool keepSelection)
        {
            DisableFileSystemWatcher();
            var selectedName = SelectedPicture?.Name;
            CancelPictureLoading();
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            Pictures.Clear();
            Polylines.Clear();
            SetupFileSystemWatcher();
            if (Settings.ShowFolders)
                foreach (var dir in Directory.EnumerateDirectories(PhotoFolderPath))
                    Pictures.Add(new PictureItemViewModel(dir, true, HandleFilePropertyChanged, null));
            await AppendFilesAsync(Directory.EnumerateFiles(PhotoFolderPath));
            if (Polylines.Count > 0)
                MapCenter = Polylines[0].Center;
            if (keepSelection && selectedName != null)
            {
                var previousSelection = Pictures.FirstOrDefault(item => item.Name == selectedName);
                if (previousSelection != null)
                    SelectItem(previousSelection);
            }
            if (SelectedPicture is null && Pictures.Count > 0)
                SelectItem(Pictures.FirstOrDefault(item => item.IsFile) ?? Pictures[0]);
            await LoadPicturesAsync();
        }

        private void DisableFileSystemWatcher()
        {
            _fileSystemWatcher?.Dispose();
            _fileSystemWatcher = null;
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

        private void HandleFileSystemWatcherRename(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var renamed = Pictures.FirstOrDefault(item => item.FullPath == e.OldFullPath);
                if (renamed != null)
                {
                    _pictureCache.Clear();
                    renamed.Renamed(e.FullPath);
                }
                else
                    HandleFileSystemWatcherChange(this, e);
            });
        }

        private void HandleFileSystemWatcherChange(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    var removed = Pictures.FirstOrDefault(item => item.FullPath == e.FullPath);
                    if (removed != null)
                        Pictures.Remove(removed);
                }
                else if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    await Task.Delay(1000);
                    var name = Path.GetFileName(e.FullPath);
                    var ext = Path.GetExtension(name).ToLowerInvariant();
                    if (File.Exists(e.FullPath) && Settings.PhotoFileExtensions.Contains(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        var newItem = new PictureItemViewModel(e.FullPath, false, HandleFilePropertyChanged, Settings);
                        if (!newItem.InsertOrdered(Pictures))
                            return;
                    }
                    else if (Directory.Exists(e.FullPath))
                    {
                        var newItem = new PictureItemViewModel(e.FullPath, true, HandleFilePropertyChanged, null);
                        if (!newItem.InsertOrdered(Pictures))
                            return;
                    }
                    else
                        return;
                    _pictureCache.Clear();
                    if (_loadPicturesTask != null)
                        _loadPicturesTask.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(LoadPicturesAsync), TaskScheduler.Default).WithExceptionLogging();
                    else
                        LoadPicturesAsync().WithExceptionLogging();
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    await Task.Delay(1000);
                    var changed = Pictures.FirstOrDefault(item => item.FullPath == e.FullPath);
                    if (changed == null)
                        return;
                    _pictureCache.Clear();
                    if (changed.IsSelected)
                        UpdatePreviewPictureAsync().WithExceptionLogging();
                    if (changed.ThumbnailImage != null)
                    {
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

        private async Task AppendFilesAsync(IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (Pictures.Any(i => i.FullPath == fileName))
                    continue;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (Settings.PhotoFileExtensions.Contains(ext, StringComparison.OrdinalIgnoreCase))
                    Pictures.Add(new PictureItemViewModel(fileName, false, HandleFilePropertyChanged, Settings));
                else if (ext == ".gpx" || ext == ".kml")
                {
                    var traces = await Task.Run(() => GpsTrace.DecodeGpsTraceFile(fileName, TimeSpan.FromMinutes(1)));
                    foreach (var trace in traces)
                        Polylines.Add(trace);
                }
            }
        }

        private async Task LoadPicturesAsync()
        {
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();

            var candidates = Pictures.Where(item => item.ThumbnailImage is null).ToArray();
            // Reorder so that we take alternately one from the top and one from the bottom
            var reordered = new PictureItemViewModel[candidates.Length];
            int iStart = 0;
            int iEnd = candidates.Length;
            for (int i = 0; i < candidates.Length; i++)
                reordered[i] = (i & 1) == 0 ? candidates[iStart++] : candidates[--iEnd];
            _loadPicturesTask = Parallel.ForEachAsync(reordered,
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = _loadCancellation.Token },
                async (item, ct) =>
                {
                    await item.LoadMetadataAndThumbnailAsync(ct);
                    if (item.IsSelected && item.GeoTag != null)
                        MapCenter = item.GeoTag;
                });
            await _loadPicturesTask;
            _loadPicturesTask = null;
        }

        private async Task WaitForPicturesLoadedAsync()
        {
            if (_loadPicturesTask != null)
                await RunProcessWithProgressBarAsync(async progressUpdate =>
                {
                    progressUpdate(-1);
                    if (_loadPicturesTask != null)
                        await _loadPicturesTask;
                    _loadPicturesTask = null;
                }, "Loading");
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
            DisableFileSystemWatcher();
        }
    }

    internal enum ViewMode
    {
        Map, Preview, Split
    }
}
