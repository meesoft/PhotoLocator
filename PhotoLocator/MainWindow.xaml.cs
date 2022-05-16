using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly MainViewModel _viewModel;
        private Point _previousMousePosition;

        public MainWindow()
        {
            InitializeComponent();
            Panel.SetZIndex(ProgressGrid, 1000);
            _viewModel = new MainViewModel();
            _viewModel.GetSelectedMapLayerName = GetSelectedMapLayerName;
            _viewModel.FocusListBoxItem = FocusListBoxItem;
            _viewModel.ViewModeCommand = new RelayCommand(s =>
                _viewModel.SelectedViewModeItem = _viewModel.SelectedViewModeItem == MapViewItem ? PreviewViewItem : MapViewItem);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            using var settings = new RegistrySettings();
            _viewModel.PhotoFileExtensions = settings.PhotoFileExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _viewModel.ShowFolders = settings.ShowFolders;
            _viewModel.SavedFilePostfix = settings.SavedFilePostfix;
            _viewModel.SlideShowInterval = settings.SlideShowInterval;
            _viewModel.ShowMetadataInSlideShow = settings.ShowMetadataInSlideShow;
            var i = settings.LeftColumnWidth;
            if (i > 10 && i < Width)
                LeftColumn.Width = new GridLength(i);
            var selectedLayer = settings.SelectedLayer;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedLayer))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            _viewModel.SelectedViewModeItem = settings.ViewMode switch
            {
                ViewMode.Preview =>  PreviewViewItem,
                ViewMode.Split =>  SplitViewItem,
                _ => MapViewItem
            };

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1]))
                Dispatcher.BeginInvoke(() =>
                {
                    _viewModel.PhotoFolderPath = Path.GetDirectoryName(args[1]);
                    _viewModel.HandleDroppedFilesAsync(args[1..]).WithExceptionShowing();
                });
            else
            {
                var savedPhotoFolderPath = settings.PhotoFolderPath;
                Dispatcher.BeginInvoke(() => _viewModel.PhotoFolderPath = savedPhotoFolderPath);
            }

            if (settings.FirstLaunch < 1)
            {
                MainViewModel.AboutCommand.Execute(null);
                settings.FirstLaunch = 1;
            }
            PictureListBox.Focus();

            Task.Run(() => CleanupTileCache(MapView.TileCachePath)).WithExceptionLogging();
        }

        private static void CleanupTileCache(string tileCachePath)
        {
            Task.Delay(4000).Wait();
            var timeThreshold = DateTime.Now - TimeSpan.FromDays(365);
            foreach (var cacheFile in new DirectoryInfo(tileCachePath).EnumerateFiles("*.*", SearchOption.AllDirectories))
                if (cacheFile.CreationTime < timeThreshold)
                    cacheFile.Delete();
        }

        private void HandleWindowClosed(object sender, EventArgs e)
        {
            using var settings = new RegistrySettings();
            if (!string.IsNullOrEmpty(_viewModel.PhotoFolderPath))
                settings.PhotoFolderPath = _viewModel.PhotoFolderPath;
            settings.ShowFolders = _viewModel.ShowFolders;
            if (_viewModel.PhotoFileExtensions != null)
                settings.PhotoFileExtensions = String.Join(",", _viewModel.PhotoFileExtensions);
            if (_viewModel.SavedFilePostfix != null)
                settings.SavedFilePostfix = _viewModel.SavedFilePostfix;
            settings.ViewMode = _viewModel.SelectedViewModeItem?.Tag as ViewMode? ?? ViewMode.Map;
            settings.SlideShowInterval = _viewModel.SlideShowInterval;
            settings.ShowMetadataInSlideShow = _viewModel.ShowMetadataInSlideShow;
            settings.LeftColumnWidth = (int)LeftColumn.Width.Value;
            settings.SelectedLayer = GetSelectedMapLayerName();
            _viewModel.Dispose();
        }

        private string? GetSelectedMapLayerName()
        {
            return Map.mapLayersMenuButton.ContextMenu.Items.Cast<MenuItem>().FirstOrDefault(i => i.IsChecked)?.Header as string;
        }

        private void FocusListBoxItem(object item)
        {
            PictureListBox.ScrollIntoView(item);
            var listBoxItem = (ListBoxItem)PictureListBox.ItemContainerGenerator.ContainerFromItem(PictureListBox.SelectedItem);
            listBoxItem.Focus();
        }

        private void HandlePictureListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.PictureSelectionChanged();
        }

        private void HandlePictureListBoxPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                _viewModel.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.XButton2 && _viewModel.SelectedPicture != null && _viewModel.SelectedPicture.IsDirectory)
            {
                _viewModel.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.XButton1)
            {
                _viewModel.ParentFolderCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void HandleFileItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _previousMousePosition = e.GetPosition(this);
        }

        private void HandleFileItemMouseMove(object sender, MouseEventArgs e)
        {
            if (PictureListBox.SelectedItem != null &&
                (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) &&
                (e.GetPosition(this) - _previousMousePosition).Length > 10)
            {
                var files = PictureListBox.SelectedItems.Cast<PictureItemViewModel>().Select(i => i.FullPath).ToArray();
                var data = new DataObject(DataFormats.FileDrop, files);
                data.SetData(DataFormats.Text, files[0]);
                DragDrop.DoDragDrop(this, data, DragDropEffects.All);
                e.Handled = true;
            }
        }

        private void HandlePictureListBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_viewModel.SelectedPicture != null && _viewModel.SelectedPicture.Name.StartsWith(e.Text, StringComparison.CurrentCultureIgnoreCase))
                return;
            var item = _viewModel.Pictures.FirstOrDefault(item => item.Name.StartsWith(e.Text, StringComparison.CurrentCultureIgnoreCase));
            if (item != null)
            {
                _viewModel.SelectItem(item);
                e.Handled = true;
            }
        }

        private void HandlePathEditPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.PhotoFolderPath = PathEdit.Text;
                e.Handled = true;
            }
        }

        private void HandleDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] droppedEntries && droppedEntries.Length > 0)
                Dispatcher.BeginInvoke(() => _viewModel.HandleDroppedFilesAsync(droppedEntries).WithExceptionShowing());
        }

        private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.PreviewPictureSource))
                InitializePreviewRenderTransform();
            else if (e.PropertyName == nameof(_viewModel.PreviewZoom))
                UpdatePreviewZoom();
        }

        private void HandleViewModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel is null)
                return;
            if (_viewModel.InSplitViewMode) // For some reason this doesn't work when done on the bound properties
            {
                MapRow.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow.Height = new GridLength(1, GridUnitType.Star);
                UpdatePreviewZoom();
            }
            else if (_viewModel.IsMapVisible)
            {
                MapRow.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow.Height = new GridLength(0, GridUnitType.Star);
            }
            else if (_viewModel.IsPreviewVisible)
            {
                MapRow.Height = new GridLength(0, GridUnitType.Star);
                PreviewRow.Height = new GridLength(1, GridUnitType.Star);
                UpdatePreviewZoom();
            }
        }

        private void HandlePreviewImageMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                _previousMousePosition = e.GetPosition(this);
                e.Handled = true;
            }
        }

        private void HandlePreviewImageMouseMove(object sender, MouseEventArgs e)
        {
            if ((e.LeftButton != MouseButtonState.Released || e.MiddleButton != MouseButtonState.Released) && _viewModel.PreviewZoom != 0 &&
                ZoomedPreviewImage.RenderTransform is MatrixTransform transform)
            {
                var pt = e.GetPosition(this);
                ZoomedPreviewImage.RenderTransform = new MatrixTransform(
                    transform.Matrix.M11, transform.Matrix.M12,
                    transform.Matrix.M21, transform.Matrix.M22,
                    transform.Matrix.OffsetX + pt.X - _previousMousePosition.X,
                    transform.Matrix.OffsetY + pt.Y - _previousMousePosition.Y);
                _previousMousePosition = pt;
                e.Handled = true;
            }
        }

        private void UpdatePreviewZoom()
        {
            ZoomToFitItem.IsChecked = _viewModel.PreviewZoom == 0;
            Zoom100Item.IsChecked = _viewModel.PreviewZoom == 1;
            Zoom200Item.IsChecked = _viewModel.PreviewZoom == 2;
            Zoom400Item.IsChecked = _viewModel.PreviewZoom == 4;
            if (_viewModel.PreviewZoom == 0)
            {
                FullPreviewImage.Visibility = Visibility.Visible;
                ZoomedPreviewCanvas.Visibility = Visibility.Collapsed;
            }
            else
            {
                FullPreviewImage.Visibility = Visibility.Collapsed;
                ZoomedPreviewCanvas.Visibility = Visibility.Visible;
                UpdateLayout();
                InitializePreviewRenderTransform();
            }
        }

        private void InitializePreviewRenderTransform()
        {
            if (_viewModel.PreviewPictureSource is null || _viewModel.PreviewZoom == 0)
                return;
            var screenDpi = VisualTreeHelper.GetDpi(this);
            var zoom = _viewModel.PreviewZoom;
            ZoomedPreviewImage.RenderTransform = new MatrixTransform(
                _viewModel.PreviewPictureSource.DpiX / screenDpi.PixelsPerInchX * zoom, 0,
                0, _viewModel.PreviewPictureSource.DpiY / screenDpi.PixelsPerInchY * zoom,
                IntMath.Round((ZoomedPreviewCanvas.ActualWidth - _viewModel.PreviewPictureSource.PixelWidth * zoom / screenDpi.PixelsPerInchX * 96) / 2),
                IntMath.Round((ZoomedPreviewCanvas.ActualHeight - _viewModel.PreviewPictureSource.PixelHeight * zoom / screenDpi.PixelsPerInchY * 96) / 2));
        }
    }
}
