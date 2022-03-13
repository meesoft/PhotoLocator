using Microsoft.Win32;
using System;

namespace PhotoLocator
{
    class RegistrySettings
    {
        public RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MeeSoft\PhotoLocator");

        public string PhotoFolderPath
        {
            get => (string?)Key.GetValue(nameof(PhotoFolderPath)) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            set => Key.SetValue(nameof(PhotoFolderPath), value ?? throw new ArgumentException("Directory cannot be null"));
        }

        public string SavedFilePostfix
        {
            get => (string?)Key.GetValue(nameof(SavedFilePostfix)) ?? "[geo]";
            set => Key.SetValue(nameof(SavedFilePostfix), value);
        }

        public int LeftColumnWidth
        {
            get => (int?)Key.GetValue(nameof(LeftColumnWidth)) ?? -1;
            set => Key.SetValue(nameof(LeftColumnWidth), value);
        }
    }
}
