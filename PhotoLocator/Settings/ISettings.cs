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

    public interface ISettings // Remember to update SettingsExtensions.AssignSettings when adding settings
    {
        string PhotoFileExtensions { get; set; }

        bool ShowFolders { get; set; }

        int ThumbnailSize { get; set; }

        bool IncludeSidecarFiles { get; set; }

        string SavedFilePostfix { get; set; }

        int JpegQuality { get; set; }

        string? ExifToolPath { get; set; }

        string? FFmpegPath { get; set; }

        public bool ForceUseExifTool { get; set; }

        int SlideShowInterval { get; set; }

        bool ShowMetadataInSlideShow { get; set; }

        BitmapScalingMode BitmapScalingMode { get; set; }

        ResamplingOptions ResamplingOptions { get; set; }

        int CropRatioNominator { get; set; }

        int CropRatioDenominator { get; set; }
    }
}
