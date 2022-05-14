using System;
using System.ComponentModel;
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
            DialogResult = true;
        }

        public string[] CleanPhotoFileExtensions()
        {
            var extensions = PhotoFileExtensions!.Replace("*", "").Replace(" ", ",").Replace(";", ",").ToLowerInvariant().
                Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < extensions.Length; i++)
                if (!extensions[i].StartsWith('.'))
                    extensions[i] = '.' + extensions[i];
            return extensions;
        }
    }
}