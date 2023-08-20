using System;
using System.Windows.Media;

namespace PhotoLocator.Settings
{
    [Flags]
    public enum ResamplingOptions
    {
        LanczosUpscaling = 1 << 1, 
        LanczosDownscaling = 1 << 2,
    }

    public interface ISettings
    {
        string PhotoFileExtensions { get; set; }

        bool ShowFolders { get; set; }

        bool IncludeSidecarFiles { get; set; }

        string SavedFilePostfix { get; set; }

        string? ExifToolPath { get; set; }

        int SlideShowInterval { get; set; }

        bool ShowMetadataInSlideShow { get; set; }

        BitmapScalingMode BitmapScalingMode { get; set; }

        ResamplingOptions ResamplingOptions { get; set; }
    }
}
