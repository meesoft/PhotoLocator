using PhotoLocator.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            Panel.SetZIndex(ProgressGrid, 1000);
            DataContext = _viewModel = new MainViewModel();
            Map.DataContext = _viewModel;
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            var settings = new RegistrySettings();
            _viewModel.PhotoFolderPath = settings.PhotoFolderPath;
            _viewModel.SavedFilePostfix = settings.SavedFilePostfix;
            var i = settings.LeftColumnWidth;
            if (i > 10 && i < Width)
                LeftColumn.Width = new GridLength(i);
            PictureListBox.Focus();

            if (settings.FirstLaunch < 1)
            {
                _viewModel.AboutCommand.Execute(null);
                settings.FirstLaunch = 1;
            }
        }

        private void HandleWindowClosed(object sender, EventArgs e)
        {
            var settings = new RegistrySettings();
            if (_viewModel.PhotoFolderPath != null)
                settings.PhotoFolderPath = _viewModel.PhotoFolderPath;
            if (_viewModel.SavedFilePostfix != null)
                settings.SavedFilePostfix = _viewModel.SavedFilePostfix;
            settings.LeftColumnWidth = (int)LeftColumn.Width.Value;
        }

        private void HandlePictureListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.PictureSelectionChanged();
        }

        private void PathEditPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _viewModel.PhotoFolderPath = PathEdit.Text;
        }
    }
}
