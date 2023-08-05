using Microsoft.Win32;
using System;
using System.Windows.Media;

namespace PhotoLocator.Settings
{
    sealed class RegistrySettings : IDisposable, IRegistrySettings
    {
        public const string DefaultPhotoFileExtensions = ".jpg, .jpeg, .cr2, .cr3, .dng";

        public RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MeeSoft\PhotoLocator");

        public object? GetValue(string name)
        {
            return Key.GetValue(name);
        }

        public void SetValue(string name, object value)
        {
            Key.SetValue(name, value);
        }

        public int FirstLaunch
        {
            get => Key.GetValue(nameof(FirstLaunch)) as int? ?? 0;
            set => Key.SetValue(nameof(FirstLaunch), value);
        }

        public string PhotoFileExtensions
        {
            get => Key.GetValue(nameof(PhotoFileExtensions)) as string ?? DefaultPhotoFileExtensions;
            set => Key.SetValue(nameof(PhotoFileExtensions), value ?? throw new ArgumentException("Filter cannot be null"));
        }

        public bool ShowFolders
        {
            get => (Key.GetValue(nameof(ShowFolders)) as int? ?? 1) != 0;
            set => Key.SetValue(nameof(ShowFolders), value ? 1 : 0);
        }

        public string PhotoFolderPath
        {
            get => Key.GetValue(nameof(PhotoFolderPath)) as string ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            set => Key.SetValue(nameof(PhotoFolderPath), value ?? throw new ArgumentException("Directory cannot be null"));
        }

        public bool IncludeSidecarFiles
        {
            get => (Key.GetValue(nameof(IncludeSidecarFiles)) as int? ?? 1) != 0;
            set => Key.SetValue(nameof(IncludeSidecarFiles), value ? 1 : 0);
        }

        public string SavedFilePostfix
        {
            get => Key.GetValue(nameof(SavedFilePostfix)) as string ?? "[geo]";
            set => Key.SetValue(nameof(SavedFilePostfix), value);
        }

        public string? ExifToolPath
        {
            get => Key.GetValue(nameof(ExifToolPath)) as string;
            set => Key.SetValue(nameof(ExifToolPath), value ?? string.Empty);
        }

        public string? SelectedLayer
        {
            get => Key.GetValue(nameof(SelectedLayer)) as string;
            set => Key.SetValue(nameof(SelectedLayer), value ?? string.Empty);
        }

        public ViewMode ViewMode
        {
            get => (ViewMode)(Key.GetValue(nameof(ViewMode)) as int? ?? (int)ViewMode.Split);
            set => Key.SetValue(nameof(ViewMode), (int)value);
        }

        public int SlideShowInterval
        {
            get => Key.GetValue(nameof(SlideShowInterval)) as int? ?? 20;
            set => Key.SetValue(nameof(SlideShowInterval), value);
        }

        public BitmapScalingMode BitmapScalingMode
        {
            get => (BitmapScalingMode)(Key.GetValue(nameof(BitmapScalingMode)) as int? ?? 1);
            set => Key.SetValue(nameof(BitmapScalingMode), (int)value);
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

        public string RenameMasks
        {
            get => Key.GetValue(nameof(RenameMasks)) as string ?? "|DT| [|*:4|]|ext|";
            set => Key.SetValue(nameof(RenameMasks), value);
        }

        public void Dispose()
        {
            Key.Dispose();
        }
    }
}
