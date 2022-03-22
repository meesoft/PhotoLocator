using MapControl;
using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using SampleApplication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace PhotoLocator
{
    internal class MainViewModel : INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        Task? _loadPicturesTask;
        bool _cancelLoading;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public MainViewModel()
        {
#if DEBUG
            if (_isInDesignMode)
            {
                Pictures.Add(new PictureItemViewModel());
                MapCenter = new Location(53.5, 8.2);
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
            if (autoTagWin.ShowDialog() == true)
            {
                if (SelectedPicture != null)
                    MapCenter = SelectedPicture.GeoTag;
                UpdatePushpins();
                PictureSelectionChanged();
            }
        });

        public ICommand CopyCommand => new RelayCommand(o =>
        {
            SavedLocation = MapCenter;
        });

        public ICommand PasteCommand => new RelayCommand(o =>
        {
            if (SavedLocation is null)
                return;
            foreach (var item in Pictures.Where(i => i.IsSelected && !Equals(i.GeoTag, SavedLocation)))
            {
                item.GeoTag = SavedLocation;
                item.GeoTagSaved = false;
            }
            UpdatePushpins();
            PictureSelectionChanged();
        });

        async Task RunProcessWithProgressBarAsync(Func<Action<double>, Task> body, string text)
        {
            using (new CursorOverride())
            {
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
                        ProgressBarValue = progress;
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
        }

        public ICommand SaveCommand => new RelayCommand(o =>
        {
            var updatedPictures = Pictures.Where(i => i.GeoTagUpdated).ToList();
            if (updatedPictures.Count == 0)
                return;
            RunProcessWithProgressBarAsync(async progressCallback =>
            {
                int i = 0;
                foreach (var item in updatedPictures)
                {
                    await item.SaveGeoTagAsync(SavedFilePostfix);
                    progressCallback((double)(++i) / updatedPictures.Count);
                }
                await Task.Delay(10);
            }, "Saving...").WithExceptionLogging();
        });

        public ICommand SlideShowCommand => new RelayCommand(o =>
        {
            if (Pictures.Count == 0)
                return;
            var slideShowWin = new SlideShowWindow(Pictures, SelectedPicture ?? Pictures.First(), SlideShowInterval, ShowMetadataInSlideShow);
            slideShowWin.Owner = App.Current.MainWindow;
            slideShowWin.ShowDialog();
            SelectedPicture = slideShowWin.SelectedPicture;
        });

        public ICommand SettingsCommand => new RelayCommand(o =>
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = App.Current.MainWindow;
            settingsWin.SavedFilePostfix = SavedFilePostfix;
            settingsWin.SlideShowInterval= SlideShowInterval;
            settingsWin.ShowMetadataInSlideShow = ShowMetadataInSlideShow;
            settingsWin.DataContext = settingsWin;
            if (settingsWin.ShowDialog() == true)
            {
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

        public async Task HandleDroppedFilesAsync(string[] droppedEntries)
        {
            await WaitForPicturesLoadedAsync();
            PhotoFolderPath = null;
            var fileNames = new List<string>(); 
            foreach (var path in droppedEntries)
                if (Directory.Exists(path))
                    await AppendFilesAsync(Directory.EnumerateFiles(path));
                else
                    fileNames.Add(path);
            await AppendFilesAsync(fileNames);
            await (_loadPicturesTask = LoadPicturesAsync());
        }

        public ICommand DeleteSelectedCommand => new RelayCommand(o =>
        {
            var selected = Pictures.Where(i => i.IsSelected).ToArray();
            if (MessageBox.Show($"Delete {selected.Length} selected file(s)?", "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
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

        public ICommand OpenInMapsCommand => new RelayCommand(o =>
        {
            if (SelectedPicture?.GeoTag is null)
            {
                MessageBox.Show("Selected file has no map coordinates.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
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
            _cancelLoading = true;
            await WaitForPicturesLoadedAsync();
            if (string.IsNullOrEmpty(PhotoFolderPath))
                return;
            Pictures.Clear();
            Polylines.Clear();
            await AppendFilesAsync(Directory.EnumerateFiles(PhotoFolderPath));
            if (Polylines.Count > 0)
                MapCenter = Polylines[0].Center;
            await (_loadPicturesTask = LoadPicturesAsync());
        }

        private async Task AppendFilesAsync(IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (Pictures.Any(i => i.FullPath == fileName))
                    continue;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (ext == ".jpg")
                    Pictures.Add(new PictureItemViewModel(fileName));
                else if (ext == ".gpx")
                {
                    var trace = await Task.Run(() => GpsTrace.DecodeGpxFile(fileName));
                    if (trace.TimeStamps.Count > 0)
                        Polylines.Add(trace);
                }
            }
        }

        private async Task LoadPicturesAsync()
        {
            _cancelLoading = false;
            foreach (var item in Pictures.Where(i => i.PreviewImage is null).ToArray())
            {
                await item.LoadImageAsync();
                if (_cancelLoading)
                    break;
            }
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
    }
}
