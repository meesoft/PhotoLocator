using System;

namespace PhotoLocator.Settings
{
    static class SettingsExtensions
    {
        public static void AssignSettings(this ISettings target, ISettings source)
        {
            target.PhotoFileExtensions = source.PhotoFileExtensions;
            target.ShowFolders = source.ShowFolders;
            target.IncludeSidecarFiles = source.IncludeSidecarFiles;
            target.SavedFilePostfix = source.SavedFilePostfix;
            target.ExifToolPath = source.ExifToolPath;
            target.SlideShowInterval = source.SlideShowInterval;
            target.BitmapScalingMode = source.BitmapScalingMode;
            target.ResamplingOptions = source.ResamplingOptions;
            target.ShowMetadataInSlideShow = source.ShowMetadataInSlideShow;
        }

        public static string[] CleanPhotoFileExtensions(this ISettings settings)
        {
            var extensions = settings.PhotoFileExtensions!.
                Replace("*", "", StringComparison.Ordinal).
                Replace(" ", ",", StringComparison.Ordinal).
                Replace(";", ",", StringComparison.Ordinal).
                ToLowerInvariant().
                Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < extensions.Length; i++)
                if (!extensions[i].StartsWith('.'))
                    extensions[i] = '.' + extensions[i];
            return extensions;
        }
    }
}
