using Microsoft.Win32;
using System;

namespace PhotoLocator
{
    class RegistrySettings
    {
        public const string DefaultPhotoFileExtensions = ".jpg";

        public RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MeeSoft\PhotoLocator");

        public string PhotoFileExtensions
        {
            get => Key.GetValue(nameof(PhotoFileExtensions)) as string ?? DefaultPhotoFileExtensions;
            set => Key.SetValue(nameof(PhotoFileExtensions), value ?? throw new ArgumentException("Filter cannot be null"));
        }

        public string PhotoFolderPath
        {
            get => Key.GetValue(nameof(PhotoFolderPath)) as string ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            set => Key.SetValue(nameof(PhotoFolderPath), value ?? throw new ArgumentException("Directory cannot be null"));
        }

        public string? SelectedLayer
        {
            get => Key.GetValue(nameof(SelectedLayer)) as string;
            set => Key.SetValue(nameof(SelectedLayer), value ?? String.Empty);
        }

        public string SavedFilePostfix
        {
            get => Key.GetValue(nameof(SavedFilePostfix)) as string ?? "[geo]";
            set => Key.SetValue(nameof(SavedFilePostfix), value);
        }

        public int SlideShowInterval
        {
            get => Key.GetValue(nameof(SlideShowInterval)) as int? ?? 20;
            set => Key.SetValue(nameof(SlideShowInterval), value);
        }

        public bool ShowMetadataInSlideShow
        {
            get => (Key.GetValue(nameof(ShowMetadataInSlideShow)) as int? ?? 0) != 0;
            set => Key.SetValue(nameof(ShowMetadataInSlideShow), value ? 1 : 0);
        }

        public int LeftColumnWidth
        {
            get => Key.GetValue(nameof(LeftColumnWidth)) as int? ?? -1;
            set => Key.SetValue(nameof(LeftColumnWidth), value);
        }

        public int FirstLaunch
        {
            get => Key.GetValue(nameof(FirstLaunch)) as int? ?? 0;
            set => Key.SetValue(nameof(FirstLaunch), value);
        }
    }
}
