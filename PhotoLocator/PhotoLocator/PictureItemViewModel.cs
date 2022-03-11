using MapControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PhotoLocator
{
    [DebuggerDisplay("Title={Title}")]
    public class PictureItemViewModel : INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif

        public event PropertyChangedEventHandler? PropertyChanged;

        public PictureItemViewModel()
        {
        }

        public PictureItemViewModel(string fileName)
        {
            FileName = fileName;
            Title = Path.GetFileName(fileName);
            Task.Run(() =>
            {
                GeoTag = ExifHandler.GetGeotag(fileName);
            }).ContinueWith(t => { Debug.WriteLine(t.Exception?.ToString()); }, TaskContinuationOptions.OnlyOnFaulted);
        }

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

        public string? Title
        {
            get => _title ?? (_isInDesignMode ? nameof(Title) : null);
            set => UpdatePropertyAndNotifyChange(ref _title, value);
        }
        string? _title;

        public string? FileName
        {
            get => _fileName;
            set => UpdatePropertyAndNotifyChange(ref _fileName, value);
        }
        string? _fileName;

        public bool IsSelected
        {
            get => _isSelected;
            set => UpdatePropertyAndNotifyChange(ref _isSelected, value);   
        }
        bool _isSelected;

        public Location? GeoTag
        {
            get => _geoTag;
            set => UpdatePropertyAndNotifyChange(ref _geoTag, value);
        }
        Location? _geoTag;
    }
}
