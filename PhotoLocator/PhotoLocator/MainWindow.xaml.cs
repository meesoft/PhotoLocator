using System;
using System.Windows;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel = new MainViewModel();
            Map.DataContext = _viewModel;
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            var settings = new RegistrySettings();
            _viewModel.PhotoFolderPath = settings.PhotoFolderPath;
        }

        private void HandleWindowClosed(object sender, EventArgs e)
        {
            var settings = new RegistrySettings();
            if (_viewModel.PhotoFolderPath != null)
                settings.PhotoFolderPath = _viewModel.PhotoFolderPath;
        }

        private void HandlePictureListBoxSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _viewModel.PictureSelectionChanged();
        }
    }
}
