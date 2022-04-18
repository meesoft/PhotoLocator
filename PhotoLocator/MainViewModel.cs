using MapControl;
using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Metadata;
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
        
        public string? SavedFilePostfix { get; set; }

        public int SlideShowInterval { get; set; }

        public bool ShowMetadataInSlideShow { get; set; }

        public string? PhotoFolderPath
        {
            get => _isInDesignMode ? nameof(PhotoFolderPath) : _photoFolderPath;
            set
            {
                if (SetProperty(ref _photoFolderPath, value))
                    LoadFolderContentsAsync().WithExceptionShowing();
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
            get => _SavedLocation;
            set
            {
                if (SetProperty(ref _SavedLocation, value))
                    UpdatePushpins();
            }
        }
        private Location? _SavedLocation;

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
                }
            }
        }
        private ComboBoxItem? _selectedViewModeItem;

        public ICommand? ViewModeCommand { get; internal set; }

        public bool IsMapVisible => Equals(SelectedViewModeItem?.Tag, ViewMode.Map) || Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public bool IsPreviewVisible => Equals(SelectedViewModeItem?.Tag, ViewMode.Preview) || Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public bool InSplitViewMode => Equals(SelectedViewModeItem?.Tag, ViewMode.Split);

        public GridLength MapRowHeight { get => _mapRowHeight; set => SetProperty(ref _mapRowHeight, value); }
        private GridLength _mapRowHeight = new(1, GridUnitType.Star);

        public GridLength PreviewRowHeight { get => _previewRowHeight; set => SetProperty(ref _previewRowHeight, value); }
        private GridLength _previewRowHeight = new(0, GridUnitType.Star);

        public ImageSource? PreviewPictureSource { get => _previewPictureSource; set => SetProperty(ref _previewPictureSource, value); }
        private ImageSource? _previewPictureSource;

        public string? PreviewPictureTitle { get => _previewPictureTitle; set => SetProperty(ref _previewPictureTitle, value); }
        private string? _previewPictureTitle;

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
                    UpdatePreviewPictureAsync().WithExceptionLogging();
                }
            }
        }
        private PictureItemViewModel? _selectedPicture;

        internal void PictureSelectionChanged()
        {
            Points.Clear();
            foreach(var item in Pictures)
                if (item != SelectedPicture && item.IsSelected && item.GeoTag != null)
                    Points.Add(new PointItem { Location = item.GeoTag, Name = item.Name });
        }

        private async Task UpdatePreviewPictureAsync()
        {
            if (SelectedPicture is null || !IsPreviewVisible)
                return;
            var selected = SelectedPicture;
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
                return title;
            });
            var maxWidth = App.Current.MainWindow.ActualWidth;
            PreviewPictureSource = await Task.Run(() => selected.LoadPreview((int)maxWidth));
            PreviewPictureTitle = await textTask;
        }

        public ICommand AutoTagCommand => new RelayCommand(async o =>
        {
            await WaitForPicturesLoadedAsync();
            if (SelectedPicture is null)
                foreach (var item in Pictures)
                    item.IsSelected = item.GeoTag is null;
            var autoTagWin = new AutoTagWindow();
            var autoTagViewModel = new AutoTagViewModel(Pictures, Polylines, () => { autoTagWin.DialogResult = true; });
            autoTagWin.Owner = App.Current.MainWindow;
            autoTagWin.DataContext = autoTagViewModel;
            PreviewPictureSource = null;
            if (autoTagWin.ShowDialog() == true)
            {
                UpdatePushpins();
                PictureSelectionChanged();
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
            foreach (var item in Pictures.Where(i => i.IsSelected && i.CanSaveGeoTag && !Equals(i.GeoTag, SavedLocation)))
            {
                item.GeoTag = SavedLocation;
                item.GeoTagSaved = false;
            }
            UpdatePushpins();
            PictureSelectionChanged();
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

        public ICommand SaveCommand => new RelayCommand(o =>
        {
            var updatedPictures = Pictures.Where(i => i.GeoTagUpdated).ToArray();
            if (updatedPictures.Length == 0)
                return;
            RunProcessWithProgressBarAsync(async progressCallback =>
            {
                int i = 0;
                await Parallel.ForEachAsync(updatedPictures, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (item, ct) =>
                {
                    await item.SaveGeoTagAsync(SavedFilePostfix);
                    progressCallback((double)Interlocked.Increment(ref i) / updatedPictures.Length);
                });
                await Task.Delay(10);
            }, "Saving...").WithExceptionLogging();
        });

        public ICommand RenameCommand => new RelayCommand(async o =>
        {
            if (SelectedPicture is null)
                return;
            if (Pictures.Any(i => i.IsSelected && i.PreviewImage is null))
                await WaitForPicturesLoadedAsync();
            var renameWin = new RenameWindow(Pictures.Where(item => item.IsSelected).ToList());
            renameWin.Owner = App.Current.MainWindow;
            renameWin.DataContext = renameWin;
            PreviewPictureSource = null;
            if (renameWin.ShowDialog() == true)
            {
                UpdatePushpins();
                PictureSelectionChanged();
            }
            UpdatePreviewPictureAsync().WithExceptionLogging();
        });

        public Func<string?>? GetSelectedMapLayerName { get; internal set; }

        public ICommand SlideShowCommand => new RelayCommand(o =>
        {
            if (Pictures.Count == 0)
                return;
            var slideShowWin = new SlideShowWindow(Pictures, SelectedPicture ?? Pictures.First(), SlideShowInterval, ShowMetadataInSlideShow,
                GetSelectedMapLayerName?.Invoke());
            slideShowWin.Owner = App.Current.MainWindow;
            slideShowWin.ShowDialog();
            SelectedPicture = slideShowWin.SelectedPicture;
        });

        public ICommand SettingsCommand => new RelayCommand(o =>
        {
            var photoFileExtensions = string.Join(", ", PhotoFileExtensions);
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = App.Current.MainWindow;
            settingsWin.PhotoFileExtensions = photoFileExtensions;
            settingsWin.SavedFilePostfix = SavedFilePostfix;
            settingsWin.SlideShowInterval= SlideShowInterval;
            settingsWin.ShowMetadataInSlideShow = ShowMetadataInSlideShow;
            settingsWin.DataContext = settingsWin;
            if (settingsWin.ShowDialog() == true)
            {
                if (settingsWin.PhotoFileExtensions != photoFileExtensions)
                {
                    PhotoFileExtensions = settingsWin.CleanPhotoFileExtensions();
                    RefreshFolderCommand.Execute(null);
                }
                SavedFilePostfix = settingsWin.SavedFilePostfix;
                SlideShowInterval = settingsWin.SlideShowInterval;
                ShowMetadataInSlideShow = settingsWin.ShowMetadataInSlideShow;
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

        public ICommand RefreshFolderCommand => new RelayCommand(o => LoadFolderContentsAsync().WithExceptionLogging());

        public Action<object>? ScrollIntoView { get; internal set; }

        public async Task HandleDroppedFilesAsync(string[] droppedEntries)
        {
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
                {
                    SelectedPicture = firstDropped;
                    ScrollIntoView?.Invoke(firstDropped);
                }
            }
            await LoadPicturesAsync();
        }

        public ICommand DeleteSelectedCommand => new RelayCommand(o =>
        {
            var selected = Pictures.Where(i => i.IsSelected).ToArray();
            if (MessageBox.Show($"Delete {selected.Length} selected file(s)?", "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            using var cursor = new CursorOverride();
            PreviewPictureSource = null;
            PreviewPictureTitle = null;
            foreach (var item in selected)
            {
                FileSystem.DeleteFile(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                Pictures.Remove(item);
            }
        });

        public ICommand ExecuteSelectedCommand => new RelayCommand(o =>
        {
            if (SelectedPicture != null)
                Process.Start(new ProcessStartInfo(SelectedPicture.FullPath) { UseShellExecute = true });
        });

        public ICommand CopyMetadataCommand => new RelayCommand(o =>
        {
            if (SelectedPicture is null)
                return;
            using var cursor = new CursorOverride();
            Clipboard.SetText(String.Join("\n", ExifHandler.EnumerateMetadata(SelectedPicture.FullPath)));
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

        private void UpdatePushpins()
        {
            Pushpins.Clear();
            if (SelectedPicture?.GeoTag != null)
                Pushpins.Add(new PointItem { Location = SelectedPicture.GeoTag, Name = SelectedPicture.Name });
            if (SavedLocation != null)
                Pushpins.Add(new PointItem { Location = SavedLocation, Name = "Saved location" });
        }

        private async Task LoadFolderContentsAsync()
        {
            _loadCancellation?.Cancel();
            await WaitForPicturesLoadedAsync();
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            Pictures.Clear();
            Polylines.Clear();
            await AppendFilesAsync(Directory.EnumerateFiles(PhotoFolderPath));
            if (Polylines.Count > 0)
                MapCenter = Polylines[0].Center;
            await LoadPicturesAsync();
        }

        private async Task AppendFilesAsync(IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (Pictures.Any(i => i.FullPath == fileName))
                    continue;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (PhotoFileExtensions.Contains(ext))
                    Pictures.Add(new PictureItemViewModel(fileName));
                else if (ext == ".gpx" || ext == ".kml")
                {
                    var trace = await Task.Run(() => GpsTrace.DecodeGpsTraceFile(fileName, TimeSpan.FromMinutes(1)));
                    if (trace.TimeStamps.Count > 0)
                        Polylines.Add(trace);
                }
            }
        }

        private async Task LoadPicturesAsync()
        {
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            _loadPicturesTask = Parallel.ForEachAsync(Pictures.Where(i => i.PreviewImage is null).ToArray(), 
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = _loadCancellation.Token }, 
                (item, ct) => item.LoadImageAsync(ct));
            await _loadPicturesTask;
            _loadPicturesTask = null;
        }

        private async Task WaitForPicturesLoadedAsync()
        {
            if (_loadPicturesTask != null)
                await RunProcessWithProgressBarAsync(async progressUpdate =>
                {
                    progressUpdate(-1);
                    await _loadPicturesTask;
                }, "Loading");
        }

        public void Dispose()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
        }
    }

    internal enum ViewMode
    {
        Map, Preview, Split
    }
}
