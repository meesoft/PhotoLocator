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
            if (!Equals(field, newValue))
            {
                field = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }

            return false;
        }

        public string? SavedFilePostfix
        {
            get => _savedFilePostfix;
            set => SetProperty(ref _savedFilePostfix, value);
        }
        string? _savedFilePostfix;

        private void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}