using PhotoLocator.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : INotifyPropertyChanged
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string? PhotoFileExtensions 
        { 
            get => _photoFileExtensions; 
            set => SetProperty(ref _photoFileExtensions, value ?? RegistrySettings.DefaultPhotoFileExtensions); 
        }
        private string? _photoFileExtensions;

        public bool ShowFolders
        {
            get => _showFolders;
            set => SetProperty(ref _showFolders, value);
        }
        bool _showFolders;

        public string? SavedFilePostfix
        {
            get => _savedFilePostfix;
            set => SetProperty(ref _savedFilePostfix, value);
        }
        string? _savedFilePostfix;

        public string? ExifToolPath
        {
            get => _exifToolPath;
            set => SetProperty(ref _exifToolPath, value);
        }
        string? _exifToolPath;

        public int SlideShowInterval
        {
            get => _slideShowInterval;
            set => SetProperty(ref _slideShowInterval, value);
        }
        int _slideShowInterval;

        public bool ShowMetadataInSlideShow
        {
            get => _showMetadataInSlideShow;
            set => SetProperty(ref _showMetadataInSlideShow, value);
        }
        bool _showMetadataInSlideShow;

        private void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            OkButton.Focus();

            if (!string.IsNullOrEmpty(SavedFilePostfix))
            { 
                foreach(var ch in Path.GetInvalidFileNameChars())
                    if (SavedFilePostfix.Contains(ch, StringComparison.Ordinal))
                        throw new UserMessageException($"Postfix contains invalid character '{ch}'");
            }

            if (!string.IsNullOrEmpty(ExifToolPath))
            {
                const string ExifToolName = "exiftool.exe";

                if (!File.Exists(ExifToolPath))
                {
                    var newPath = Path.Combine(ExifToolPath, ExifToolName);
                    if (File.Exists(newPath))
                        ExifToolPath = newPath;
                    else
                        throw new UserMessageException("ExifTool not found in specified path");
                }
                else if (!Path.GetFileName(ExifToolPath).Equals(ExifToolName, StringComparison.OrdinalIgnoreCase))
                    throw new UserMessageException($"ExifTool executable must be named '{ExifToolName}'");
            }

            DialogResult = true;
        }

        public string[] CleanPhotoFileExtensions()
        {
            var extensions = PhotoFileExtensions!.
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