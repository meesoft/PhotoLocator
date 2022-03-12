using MapControl;
using PhotoLocator.Metadata;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    [DebuggerDisplay("Title={Title}")]
    public class PictureItemViewModel : INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        public event PropertyChangedEventHandler? PropertyChanged;

        public PictureItemViewModel()
        {
            if (_isInDesignMode)
            {
                Title = nameof(PictureItemViewModel);
                GeoTagSaved = true;
            }
        }

        public PictureItemViewModel(string fileName)
        {
            Title = Path.GetFileName(fileName);
            FileName = fileName;
        }

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

        public string? Title
        {
            get => _title ?? (_isInDesignMode ? nameof(Title) : null);
            set => SetProperty(ref _title, value);
        }
        string? _title;

        public string? FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }
        string? _fileName;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        bool _isSelected;

        public bool GeoTagSaved
        {
            get => _geoTagSaved;
            set => SetProperty(ref _geoTagSaved, value);
        }
        bool _geoTagSaved;

        public bool GeoTagUpdated
        {
            get => _geoTagUpdated;
            set => SetProperty(ref _geoTagUpdated, value);
        }
        bool _geoTagUpdated;

        public Location? GeoTag
        {
            get => _geoTag;
            set => SetProperty(ref _geoTag, value);
        }
        Location? _geoTag;

        public ImageSource? PreviewImage 
        { 
            get => _previewImage; 
            set => SetProperty(ref _previewImage, value);
        }
        private ImageSource? _previewImage;

        public void LoadImage()
        {
            try
            {
                GeoTag = ExifHandler.GetGeotag(FileName ?? throw new InvalidOperationException("FileName not set"));
                GeoTagSaved = GeoTag != null;
                GeoTagUpdated = false;

                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.UriSource = new Uri(FileName);
                thumbnail.DecodePixelWidth = 150;
                thumbnail.EndInit();
                thumbnail.Freeze();
                PreviewImage = thumbnail;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}
