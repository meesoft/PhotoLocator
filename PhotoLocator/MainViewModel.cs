using MapControl;
using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using SampleApplication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
            if (_isInDesignMode)
                Pictures.Add(new PictureItemViewModel());
        }

        public bool IsProgressBarVisible { get => _isProgressBarVisible; set => SetProperty(ref _isProgressBarVisible, value); }
        private bool _isProgressBarVisible;

        public TaskbarItemProgressState TaskbarProgressState { get => _taskbarProgressState; set => SetProperty(ref _taskbarProgressState, value); }
        TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;

        public double ProgressBarValue { get => _progressBarValue; set => SetProperty(ref _progressBarValue, value); }
        private double _progressBarValue;

        public string? ProgressBarText { get => _progressBarText; set => SetProperty(ref _progressBarText, value); }
        private string? _progressBarText;

        public bool IsWindowEnabled { get => _isWindowEnabled; set => SetProperty(ref _isWindowEnabled, value); }
        private bool _isWindowEnabled = true;
        
        public string? SavedFilePostfix { get; set; }

        public int SlideShowInterval { get; set; }

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

        public ICommand AutoTagCommand => new RelayCommand(o =>
        {
            if (SelectedPicture is null)
                foreach (var item in Pictures)
                    item.IsSelected = true;
            var autoTagWin = new AutoTagWindow();
            var autoTagViewModel = new AutoTagViewModel(Pictures, Polylines, () => { autoTagWin.DialogResult = true; });
            autoTagWin.Owner = App.Current.MainWindow;
            autoTagWin.DataContext = autoTagViewModel;
            if (autoTagWin.ShowDialog() == true)
            {
                MapCenter = SelectedPicture?.GeoTag;
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

        private CancellationTokenSource? _cancellation;

        async Task RunProcessWithProgressBarAsync(Func<Action<double>, Task> body, string? text = null)
        {
            using (new CursorOverride())
            {
                ProgressBarValue = 0;
                TaskbarProgressState = TaskbarItemProgressState.Normal;
                ProgressBarText = text;
                IsProgressBarVisible = true;
                _cancellation = new CancellationTokenSource();
                IsWindowEnabled = false;
                try
                {
                    await body(progress =>
                    {
                        _cancellation.Token.ThrowIfCancellationRequested();
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
                    _cancellation.Dispose();
                    _cancellation = null;
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
            var slideShowWin = new SlideShowWindow(Pictures, SelectedPicture ?? Pictures.First(), SlideShowInterval);
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
            settingsWin.DataContext = settingsWin;
            if (settingsWin.ShowDialog() == true)
            {
                SavedFilePostfix = settingsWin.SavedFilePostfix;
                SlideShowInterval = settingsWin.SlideShowInterval;
            }
        });

        public ICommand AboutCommand => new RelayCommand(o =>
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
            PhotoFolderPath = null;
            var fileNames = new List<string>(); 
            foreach (var path in droppedEntries)
                if (Directory.Exists(path))
                    await AppendFilesAsync(Directory.EnumerateFiles(path));
                else
                    fileNames.Add(path);
            await AppendFilesAsync(fileNames);
            await LoadPicturesAsync();
        }

        public ICommand DeleteSelectedCommand => new RelayCommand(o =>
        {
            if (MessageBox.Show("Delete selected files?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            foreach (var item in Pictures.Where(i => i.IsSelected).ToArray())
            {
                FileSystem.DeleteFile(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                Pictures.Remove(item);
            }
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
            foreach (var item in Pictures.Where(i => i.PreviewImage is null).ToArray())
                await item.LoadImageAsync();
        }
    }
}
