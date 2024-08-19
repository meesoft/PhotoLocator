using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PhotoLocator.Settings
{
    public sealed class ObservableSettings : ISettings, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string PhotoFileExtensions
        {
            get => _photoFileExtensions;
            set => SetProperty(ref _photoFileExtensions, value ?? RegistrySettings.DefaultPhotoFileExtensions);
        }
        private string _photoFileExtensions = RegistrySettings.DefaultPhotoFileExtensions;

        public bool ShowFolders
        {
            get => _showFolders;
            set => SetProperty(ref _showFolders, value);
        }
        bool _showFolders;

        public int ThumbnailSize
        {
            get => _thumbnailSize;
            set => SetProperty(ref _thumbnailSize, value);
        }
        int _thumbnailSize;

        public bool IncludeSidecarFiles
        {
            get => _includeSidecarFiles;
            set => SetProperty(ref _includeSidecarFiles, value);
        }
        bool _includeSidecarFiles;

        public string SavedFilePostfix
        {
            get => _savedFilePostfix;
            set => SetProperty(ref _savedFilePostfix, value);
        }
        string _savedFilePostfix = string.Empty;

        public string? ExifToolPath
        {
            get => _exifToolPath;
            set => SetProperty(ref _exifToolPath, value);
        }
        string? _exifToolPath;

        public string? FFmpegPath
        {
            get => _ffmpegPath;
            set => SetProperty(ref _ffmpegPath, value);
        }
        string? _ffmpegPath;

        public int SlideShowInterval
        {
            get => _slideShowInterval;
            set => SetProperty(ref _slideShowInterval, value);
        }
        int _slideShowInterval;

        public bool ShowMetadataInSlideShow
        {
            get => _showMetadataInSlideShow;
            set => SetProperty(ref _showMetadataInSlideShow, value);
        }
        bool _showMetadataInSlideShow;

        public BitmapScalingMode BitmapScalingMode
        {
            get => _bitmapScalingMode;
            set => SetProperty(ref _bitmapScalingMode, value);
        }
        BitmapScalingMode _bitmapScalingMode;

        public ResamplingOptions ResamplingOptions
        {
            get => _resamplingOptions;
            set => SetProperty(ref _resamplingOptions, value);
        }
        ResamplingOptions _resamplingOptions;

        public bool LanczosUpscaling
        {
            get => (ResamplingOptions & ResamplingOptions.LanczosUpscaling) != 0;
            set => SetProperty(ref _resamplingOptions, value
                ? _resamplingOptions | ResamplingOptions.LanczosUpscaling
                : _resamplingOptions & ~ResamplingOptions.LanczosUpscaling);
        }

        public bool LanczosDownscaling
        {
            get => (ResamplingOptions & ResamplingOptions.LanczosDownscaling) != 0;
            set => SetProperty(ref _resamplingOptions, value
                ? _resamplingOptions | ResamplingOptions.LanczosDownscaling
                : _resamplingOptions & ~ResamplingOptions.LanczosDownscaling);
        }

        public int CropRatioNominator
        {
            get => _cropRatioNominator;
            set => SetProperty(ref _cropRatioNominator, value);
        }
        int _cropRatioNominator;

        public int CropRatioDenominator
        {
            get => _cropRatioDenominator;
            set => SetProperty(ref _cropRatioDenominator, value);
        }
        int _cropRatioDenominator;

        public double CropWidthHeightRatio => CropRatioDenominator > 0 ? (double)CropRatioNominator / CropRatioDenominator : 0;
    }
}
