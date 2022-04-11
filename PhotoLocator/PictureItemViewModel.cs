using MapControl;
using PhotoLocator.Metadata;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    [DebuggerDisplay("Name={Name}")]
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
#if DEBUG
            if (_isInDesignMode)
            {
                Name = nameof(PictureItemViewModel);
                GeoTag = new Location(0, 0);
                GeoTagSaved = true;
            }
#endif
        }

        public PictureItemViewModel(string fileName)
        {
            Name = Path.GetFileName(fileName);
            FullPath = fileName;
        }

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string? Name
        {
            get => _name ?? (_isInDesignMode ? nameof(Name) : null);
            set => SetProperty(ref _name, value);
        }
        string? _name;

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }
        string _fullPath = String.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        bool _isSelected;

        public bool GeoTagSaved
        {
            get => _geoTagSaved;
            set
            {
                if (SetProperty(ref _geoTagSaved, value))
                    NotifyPropertyChanged(nameof(GeoTagUpdated));
            }
        }
        bool _geoTagSaved;

        public bool GeoTagUpdated
        {
            get => GeoTag != null && !GeoTagSaved;
        }

        public bool GeoTagPresent
        {
            get => GeoTag != null;
        }

        public Location? GeoTag
        {
            get => _geoTag;
            set
            {
                if (SetProperty(ref _geoTag, value))
                {
                    NotifyPropertyChanged(nameof(GeoTagUpdated));
                    NotifyPropertyChanged(nameof(GeoTagPresent));
                }
            }
        }
        Location? _geoTag;

        public DateTime? TimeStamp
        {
            get => _timeStamp;
            set => SetProperty(ref _timeStamp, value);
        }
        DateTime? _timeStamp;

        public ImageSource? PreviewImage 
        { 
            get => _previewImage; 
            set => SetProperty(ref _previewImage, value);
        }
        private ImageSource? _previewImage;

        public string? ErrorMessage 
        { 
            get => _errorMessage; 
            set => SetProperty(ref _errorMessage, value); 
        }
        private string? _errorMessage;

        public bool CanSaveGeoTag => Path.GetExtension(Name)?.ToLowerInvariant() == ".jpg";

        public async ValueTask LoadImageAsync(CancellationToken ct)
        {
            try
            {
                GeoTag = await Task.Run(() =>
                {
                    using var file = File.OpenRead(FullPath);
                    var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    if (decoder.Frames[0].Metadata is not BitmapMetadata metadata)
                        return null;
                    if (DateTime.TryParse(metadata.DateTaken, out var dateTaken))
                        _timeStamp = DateTime.SpecifyKind(dateTaken, DateTimeKind.Local);
                    return ExifHandler.GetGeotag(metadata);
                }, ct);
                GeoTagSaved = GeoTag != null;

                PreviewImage = await Task.Run(() =>
                {
                    var thumbnail = new BitmapImage();
                    thumbnail.BeginInit();
                    thumbnail.UriSource = new Uri(FullPath);
                    thumbnail.DecodePixelWidth = 200;
                    thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                    thumbnail.EndInit();
                    thumbnail.Freeze();
                    return thumbnail;
                }, ct);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
        }

        internal async Task SaveGeoTagAsync(string? postfix)
        {
            try
            {
                await Task.Run(() =>
                {
                    var newFileName = string.IsNullOrEmpty(postfix) ? FullPath :
                        Path.Combine(Path.GetDirectoryName(FullPath)!, Path.GetFileNameWithoutExtension(FullPath)) + postfix + Path.GetExtension(FullPath);
                    ExifHandler.SetGeotag(FullPath, newFileName, GeoTag ?? throw new InvalidOperationException(nameof(GeoTag) + " not set"));
                });
                GeoTagSaved = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
        }
    }
}
