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

        public bool IsOkButtonEnabled { get => _isOkButtonEnabled; set => SetProperty(ref _isOkButtonEnabled, value); }
        private bool _isOkButtonEnabled = true;

        public ICommand OkCommand => new RelayCommand(o => DialogResult = true);

        private void InitializeWindow(string label, string? title, string? defaultText)
        {
            Owner = App.Current?.MainWindow;
            Title = title;
            Label = label;
            Text = defaultText;
            DataContext = this;
        }

        public static string? Show(string label, string? title = null, string? defaultText = null)
        {
            var window = new TextInputWindow();
            window.InitializeWindow(label, title, defaultText);
            window.ShowDialog();
            window.DataContext = null;
            return window.DialogResult is true ? window.Text : null;
        }

        public static string? Show(string label, Func<string?, bool> textChanged, string? title = null, string? defaultText = null)
        {
            ArgumentNullException.ThrowIfNull(textChanged);
            var window = new TextInputWindow();
            window.IsOkButtonEnabled = textChanged(defaultText);
            window.InitializeWindow(label, title, defaultText);

            void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Text))
                    window.IsOkButtonEnabled = textChanged(window.Text);
            }
            window.PropertyChanged += HandlePropertyChanged;
            window.ShowDialog();
            window.PropertyChanged -= HandlePropertyChanged;
            window.DataContext = null;
            return window.DialogResult is true ? window.Text : null;
        }
    }
}
