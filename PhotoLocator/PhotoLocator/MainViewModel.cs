using MapControl;
using SampleApplication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

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

        public string? PhotoFolderPath
        {
            get => _isInDesignMode ? nameof(PhotoFolderPath) : _photoFolderPath;
            set
            {
                if (SetProperty(ref _photoFolderPath, value))
                    LoadFolder();
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

        public IEnumerable<PictureItemViewModel> FolderPictures
        {
            get => _isInDesignMode ? new[] { new PictureItemViewModel() } : _folderPictures;
            set
            {
                _folderPictures = value;
                NotifyPropertyChanged();
            }
        }
        private IEnumerable<PictureItemViewModel> _folderPictures = Enumerable.Empty<PictureItemViewModel>();

        public PictureItemViewModel? SelectedPicture
        {
            get => _selectedPicture;
            set
            {
                if (SetProperty(ref _selectedPicture, value))
                {
                    Pushpins.Clear();
                    if (value?.GeoTag != null)
                    {
                        MapCenter = value.GeoTag;
                        Pushpins.Add(new PointItem { Location = MapCenter, Name = value.Title }); 
                    }
                }
            }
        }
        private PictureItemViewModel? _selectedPicture;

        internal void PictureSelectionChanged()
        {
            Points.Clear();
            foreach(var item in FolderPictures)
                if (item != SelectedPicture && item.IsSelected && item.GeoTag != null)
                    Points.Add(new PointItem { Location = item.GeoTag, Name = item.Title });
        }

        private void LoadFolder()
        {
            if (PhotoFolderPath is null)
                return;
            var items = new List<PictureItemViewModel>();
            foreach (var fileName in Directory.GetFiles(PhotoFolderPath, "*.jpg"))
                items.Add(new PictureItemViewModel(fileName));
            FolderPictures = items;
            LoadPictures();
        }

        private void LoadPictures()
        {
            Task.Run(() =>
            {
                Task.Delay(500).Wait();
                Parallel.ForEach(FolderPictures.Where(p => p.PreviewImage is null), new ParallelOptions { MaxDegreeOfParallelism = 1 },
                    item => item.LoadImage());
            }).ContinueWith(t => { Debug.WriteLine(t.Exception?.ToString()); }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
