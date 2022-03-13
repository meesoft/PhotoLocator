using Microsoft.Win32;
using System;

namespace PhotoLocator
{
    class RegistrySettings
    {
        public const string PhotoFolderPathValue = "PhotoFolderPath";
        public const string SavedFilePostfixValue = "SavedFilePostfix";

        public RegistryKey Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MeeSoft\PhotoLocator");

        public string PhotoFolderPath
        {
            get => (string?)Key.GetValue(PhotoFolderPathValue) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            set => Key.SetValue(PhotoFolderPathValue, value ?? throw new ArgumentException("Directory cannot be null"));
        }

        public string SavedFilePostfix
        {
            get => (string?)Key.GetValue(SavedFilePostfixValue) ?? "[geo]";
            set => Key.SetValue(SavedFilePostfixValue, value);
        }
    }
}
