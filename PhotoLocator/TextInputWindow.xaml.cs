using PhotoLocator.Helpers;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for TextInputWindow.xaml
    /// </summary>
    public partial class TextInputWindow : Window, INotifyPropertyChanged
    {
        public TextInputWindow()
        {
            InitializeComponent();
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            TextBox.SelectAll();
            TextBox.Focus();
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

        public string? Label { get => _label; set => SetProperty(ref _label, value); }
        private string? _label;

        public string? Text { get => _text; set => SetProperty(ref _text, value); }
        private string? _text;

        public ICommand OkCommand => new RelayCommand(o => DialogResult = true);

        public static string? Show(string label, string? title = null, string? defaultText = null)
        {
            var window = new TextInputWindow();
            window.Owner = App.Current.MainWindow;
            window.Title = title;
            window.Label = label;
            window.Text = defaultText;
            window.DataContext = window;
            window.ShowDialog();
            window.DataContext = null;
            return window.DialogResult == true ? window.Text : null;
        }

        public static bool Show(string label, Action<string?> textUpdated, string? title = null, string? defaultText = null)
        {
            var window = new TextInputWindow();
            window.Owner = App.Current.MainWindow;
            window.Title = title;
            window.Label = label;
            window.Text = defaultText;
            window.DataContext = window;
            window.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Text))
                    textUpdated(window.Text);
            };
            window.ShowDialog();
            window.DataContext = null;
            return window.DialogResult == true;
        }
    }
}
