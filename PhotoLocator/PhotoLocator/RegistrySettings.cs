using Microsoft.Win32;
using System;

namespace PhotoLocator
{
    class RegistrySettings
    {
        public RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MeeSoft\PhotoLocator");

        public string PhotoFolderPath
        {
            get => Key.GetValue(nameof(PhotoFolderPath)) as string ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            set => Key.SetValue(nameof(PhotoFolderPath), value ?? throw new ArgumentException("Directory cannot be null"));
        }

        public string SavedFilePostfix
        {
            get => Key.GetValue(nameof(SavedFilePostfix)) as string ?? "[geo]";
            set => Key.SetValue(nameof(SavedFilePostfix), value);
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
