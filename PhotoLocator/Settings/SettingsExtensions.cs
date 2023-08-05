namespace PhotoLocator.Settings
{
    static class SettingsExtensions
    {
        public static void AssignSettings(this ISettings target, ISettings source)
        {
            target.PhotoFileExtensions = source.PhotoFileExtensions;
            target.ShowFolders = source.ShowFolders;
            target.SavedFilePostfix = source.SavedFilePostfix;
            target.ExifToolPath = source.ExifToolPath;
            target.SlideShowInterval = source.SlideShowInterval;
            target.BitmapScalingMode = source.BitmapScalingMode;
            target.ShowMetadataInSlideShow = source.ShowMetadataInSlideShow;
        }
    }
}
