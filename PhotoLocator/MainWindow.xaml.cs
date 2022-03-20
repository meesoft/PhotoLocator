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
        readonly MainViewModel _viewModel;

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
            _viewModel.SlideShowInterval = settings.SlideShowInterval;
            _viewModel.ShowMetadataInSlideShow = settings.ShowMetadataInSlideShow;
            var i = settings.LeftColumnWidth;
            if (i > 10 && i < Width)
                LeftColumn.Width = new GridLength(i);
            PictureListBox.Focus();

            if (settings.FirstLaunch < 1)
            {
                MainViewModel.AboutCommand.Execute(null);
                settings.FirstLaunch = 1;
            }
        }

        private void HandleWindowClosed(object sender, EventArgs e)
        {
            var settings = new RegistrySettings();
            if (!string.IsNullOrEmpty(_viewModel.PhotoFolderPath))
                settings.PhotoFolderPath = _viewModel.PhotoFolderPath;
            if (_viewModel.SavedFilePostfix != null)
                settings.SavedFilePostfix = _viewModel.SavedFilePostfix;
            settings.SlideShowInterval = _viewModel.SlideShowInterval;
            settings.ShowMetadataInSlideShow = _viewModel.ShowMetadataInSlideShow;
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

        private void HandleDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedEntries && droppedEntries.Length > 0)
                Dispatcher.BeginInvoke(() => _viewModel.HandleDroppedFilesAsync(droppedEntries).WithExceptionShowing());
        }
    }
}
