using MapControl;
using PhotoLocator.Helpers;
using SampleApplication;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
            return false;
        }

        public MainViewModel()
        {
            if (_isInDesignMode)
                FolderPictures.Add(new PictureItemViewModel());
        }

        public string? PhotoFolderPath
        {
            get => _isInDesignMode ? nameof(PhotoFolderPath) : _photoFolderPath;
            set
            {
                if (SetProperty(ref _photoFolderPath, value))
                    LoadFolderContents();
            }
        }
        private string? _photoFolderPath;

        public ObservableCollection<PointItem> Points { get; } = new ObservableCollection<PointItem>();
        public ObservableCollection<PointItem> Pushpins { get; } = new ObservableCollection<PointItem>();
        public ObservableCollection<PolylineItem> Polylines { get; } = new ObservableCollection<PolylineItem>();

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

        public ObservableCollection<PictureItemViewModel> FolderPictures { get; } = new ObservableCollection<PictureItemViewModel>();

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
            foreach(var item in FolderPictures)
                if (item != SelectedPicture && item.IsSelected && item.GeoTag != null)
                    Points.Add(new PointItem { Location = item.GeoTag, Name = item.Name });
        }

        Location? GetBestGeoFix(DateTime timeStamp, TimeSpan maxTimeDifference)
        {
            Location? bestFix = null;
            var minDist = maxTimeDifference + TimeSpan.FromMilliseconds(1);
            foreach (var geoFix in FolderPictures.Where(i => i.GeoTag != null && i.TimeStamp.HasValue))
            {
                var dist = (timeStamp - geoFix.TimeStamp!.Value).Duration();
                if (dist < minDist)
                {
                    minDist = dist;
                    bestFix = geoFix.GeoTag;
                }
            }
            return bestFix;
        }

        public ICommand AutoTagCommand
        {
            get => new RelayCommand(o =>
            {
                if (SelectedPicture is null)
                    MessageBox.Show("No photos selected");
                foreach (var item in FolderPictures.Where(i => i.IsSelected && i.GeoTag is null && i.TimeStamp.HasValue))
                {
                    var bestFix = GetBestGeoFix(item.TimeStamp!.Value, TimeSpan.FromMinutes(15));
                    if (bestFix != null)
                    {
                        item.GeoTag = bestFix;
                        item.GeoTagSaved = false;
                    }
                }
                UpdatePushpins();
                PictureSelectionChanged();
            });
        }

        public ICommand CopyCommand 
        {
            get => new RelayCommand(o =>
            {
                SavedLocation = MapCenter;
            });
        }

        public ICommand PasteCommand
        {
            get => new RelayCommand(o =>
            {
                if (SavedLocation is null)
                    return;
                foreach (var item in FolderPictures.Where(i => i.IsSelected && !Equals(i.GeoTag, SavedLocation)))
                {
                    item.GeoTag = SavedLocation;
                    item.GeoTagSaved = false;
                }
                UpdatePushpins();
                PictureSelectionChanged();
            });
        }

        public string? SavedFilePostfix { get; set; }

        public ICommand SaveCommand
        {
            get => new RelayCommand(o =>
            {
                foreach (var item in FolderPictures.Where(i => i.GeoTagUpdated))
                    item.SaveGeoTag(SavedFilePostfix);
            });
        }

        public ICommand SettingsCommand
        {
            get => new RelayCommand(o =>
            {
                var settingsWin = new SettingsWindow();
                settingsWin.Owner = App.Current.MainWindow;
                settingsWin.SavedFilePostfix = SavedFilePostfix;
                settingsWin.DataContext = settingsWin;
                if (settingsWin.ShowDialog() == true)
                {
                    SavedFilePostfix = settingsWin.SavedFilePostfix;
                }
            });
        }

        public ICommand AboutCommand
        {
            get => new RelayCommand(o =>
            {
                var aboutWin = new AboutWindow();
                aboutWin.Owner = App.Current.MainWindow;
                aboutWin.ShowDialog();
            });
        }

        public ICommand BrowseForPhotosCommand
        {
            get => new RelayCommand(o =>
            {
                var browser = new System.Windows.Forms.FolderBrowserDialog();
                browser.InitialDirectory = PhotoFolderPath;
                if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    PhotoFolderPath = browser.SelectedPath;
            });
        }

        private void UpdatePushpins()
        {
            Pushpins.Clear();
            if (SelectedPicture?.GeoTag != null)
                Pushpins.Add(new PointItem { Location = SelectedPicture.GeoTag, Name = SelectedPicture.Name });
            if (SavedLocation != null)
                Pushpins.Add(new PointItem { Location = SavedLocation, Name = "Saved location" });
        }

        private void LoadFolderContents()
        {
            if (PhotoFolderPath is null)
                return;
            FolderPictures.Clear();
            foreach (var fileName in Directory.EnumerateFiles(PhotoFolderPath, "*.jpg"))
                FolderPictures.Add(new PictureItemViewModel(fileName));
            LoadPictures();
        }

        private void LoadPictures()
        {
            LoadPicturesAsync().ContinueWith(t => { Debug.WriteLine(t.Exception?.ToString()); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task LoadPicturesAsync()
        {
            foreach (var item in FolderPictures.Where(i => i.PreviewImage is null))
                await item.LoadImageAsync();
        }
    }
}
