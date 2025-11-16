using PhotoLocator.Helpers;
using System;
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
            get;
            set => SetProperty(ref field, value ?? RegistrySettings.DefaultPhotoFileExtensions);
        } = RegistrySettings.DefaultPhotoFileExtensions;

        public bool ShowFolders
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int ThumbnailSize
        {
            get;
            set => SetProperty(ref field, Math.Clamp(value, 32, 1024));
        }

        public bool IncludeSidecarFiles
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string SavedFilePostfix
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public int JpegQuality
        {
            get;
            set => SetProperty(ref field, Math.Clamp(value, 1, 100));
        }

        public string? ExifToolPath
        {
            get;
            set => SetProperty(ref field, value?.TrimPath());
        }

        public string? FFmpegPath
        {
            get;
            set => SetProperty(ref field, value?.TrimPath());
        }

        public bool ForceUseExifTool
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int SlideShowInterval
        {
            get;
            set => SetProperty(ref field, Math.Max(1, value));
        }

        public bool ShowMetadataInSlideShow
        {
            get;
            set => SetProperty(ref field, value);
        }

        public BitmapScalingMode BitmapScalingMode
        {
            get;
            set => SetProperty(ref field, value);
        }

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

        public bool TrackZoom
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int CropRatioNominator
        {
            get;
            set => SetProperty(ref field, Math.Max(0, value));
        }

        public int CropRatioDenominator
        {
            get;
            set => SetProperty(ref field, Math.Max(0, value));
        }

        public double CropWidthHeightRatio => CropRatioDenominator > 0 ? (double)CropRatioNominator / CropRatioDenominator : 0;
    }
}
