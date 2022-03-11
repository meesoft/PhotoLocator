using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PhotoLocator
{
    internal class MainViewModel : INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        public event PropertyChangedEventHandler? PropertyChanged;

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void UpdatePropertyAndNotifyChange<T>(ref T? field, T? value, [CallerMemberName] string? propertyName = null) where T : IEquatable<T>
        {
            if (field != null && field.Equals(value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string? PhotoFolderPath
        {
            get => _photoFolderPath;
            set
            {
                UpdatePropertyAndNotifyChange(ref _photoFolderPath, value);
                LoadPictures();
            }
        }

        public MapControl.Location MapCenter { get; set; } = new MapControl.Location(53.5, 8.2);

        private void LoadPictures()
        {
            if (PhotoFolderPath is null)
                return;
            try
            {
                var items = new List<PictureItemViewModel>();
                foreach (var fileName in Directory.GetFiles(PhotoFolderPath, "*.jpg"))
                {
                    items.Add(new PictureItemViewModel { Title = Path.GetFileName(fileName) });
                }
                FolderPictures = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private string? _photoFolderPath;

        public IEnumerable<PictureItemViewModel>? FolderPictures 
        { 
            get => _isInDesignMode ? new[] { new PictureItemViewModel { Title = "Picture" } } : _folderPictures; 
            set
            {
                _folderPictures = value;
                NotifyPropertyChanged();
            }
        }
        private IEnumerable<PictureItemViewModel>? _folderPictures;
    }
}
