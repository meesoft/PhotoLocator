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
using System.Windows.Threading;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        readonly MainViewModel _viewModel;
        Point _previousMousePosition;
        bool _isDraggingPreview, _isStartingFileItemDrag;
        int _selectStartIndex;

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
            _viewModel.ExifToolPath = settings.ExifToolPath;
            _viewModel.SlideShowInterval = settings.SlideShowInterval;
            _viewModel.BitmapScalingMode = settings.BitmapScalingMode;
            _viewModel.ShowMetadataInSlideShow = settings.ShowMetadataInSlideShow;
            var i = settings.LeftColumnWidth;
            if (i > 10 && i < Width)
                LeftColumn.Width = new GridLength(i);
            var selectedLayer = settings.SelectedLayer;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedLayer))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Map.MapItemSelected += _viewModel.HandleMapItemSelected;
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
                if (!Directory.Exists(savedPhotoFolderPath))
                    savedPhotoFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
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
            settings.ExifToolPath = _viewModel.ExifToolPath;
            settings.ViewMode = _viewModel.SelectedViewModeItem?.Tag as ViewMode? ?? ViewMode.Map;
            settings.SlideShowInterval = _viewModel.SlideShowInterval;
            settings.BitmapScalingMode = _viewModel.BitmapScalingMode;
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
            var listBoxItem = (ListBoxItem)PictureListBox.ItemContainerGenerator.ContainerFromItem(item);
            listBoxItem?.Focus();
        }

        private void HandlePictureListBoxPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton2 && _viewModel.SelectedPicture != null && _viewModel.SelectedPicture.IsDirectory)
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

        private void HandlePictureListBoxPreviewMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                || e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                _viewModel.ShellContextMenuCommand.Execute(null);
            }
        }

        private void HandlePictureListBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (SelectItem(e.Text, PictureListBox.SelectedIndex + 1, _viewModel.Pictures.Count) ||
                SelectItem(e.Text, 0, PictureListBox.SelectedIndex))
                e.Handled = true;

            bool SelectItem(string text, int min, int max)
            {
                for (int i = min; i < max; i++)
                    if (_viewModel.Pictures[i].Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _viewModel.SelectItem(_viewModel.Pictures[i]);
                        return true;
                    }
                return false;
            }
        }

        private void HandlePictureListBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var selecteditem = _viewModel.SelectedPicture;
            if (selecteditem is null)
                return;
            if (e.Key == Key.Space)
            {
                selecteditem.IsChecked = !selecteditem.IsChecked;
                e.Handled = true;
            }
            else if (e.Key == Key.Insert)
            {
                if (!(e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift)))
                    selecteditem.IsChecked = !selecteditem.IsChecked;
                if (PictureListBox.SelectedIndex < PictureListBox.Items.Count - 1)
                {
                    PictureListBox.SelectedItem = PictureListBox.Items[PictureListBox.SelectedIndex + 1];
                    FocusListBoxItem(PictureListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.End) // Compensate for strange bug in WPF that End sometimes doesn't go to the last item
            {
                PictureListBox.SelectedItem = PictureListBox.Items[^1];
                FocusListBoxItem(PictureListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                _selectStartIndex = Math.Max(0, PictureListBox.SelectedIndex);
            }
            if (e.Key == Key.Escape) // Prevent Escape from navigating to the first item
                e.Handled = true;
        }

        private void HandlePictureListBoxPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Home || e.Key == Key.End || e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right ) && 
                (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift)) && PictureListBox.SelectedIndex >= 0)
            {
                var last = Math.Max(_selectStartIndex, PictureListBox.SelectedIndex);
                for (int i = Math.Min(_selectStartIndex, PictureListBox.SelectedIndex); i <= last; i++)
                    ((PictureItemViewModel)PictureListBox.Items[i]).IsChecked = true;
                Dispatcher.BeginInvoke(() => _viewModel.UpdatePoints(), DispatcherPriority.ApplicationIdle); // This can take some time
            }
        }

        private void HandleFileItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && sender is PictureItemView itemView && itemView.DataContext is PictureItemViewModel item)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    var clickedIndex = PictureListBox.Items.IndexOf(item);
                    var selectedIndex = Math.Max(0, PictureListBox.SelectedIndex);
                    var last = Math.Max(clickedIndex, selectedIndex);
                    for (int i = Math.Min(clickedIndex, selectedIndex); i <= last; i++)
                        ((PictureItemViewModel)PictureListBox.Items[i]).IsChecked = true;
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    item.IsChecked = !item.IsChecked;
                }
                else if (e.ClickCount == 2)
                {
                    _viewModel.ExecuteSelectedCommand.Execute(null);
                    e.Handled = true;
                }
                else
                    _isStartingFileItemDrag = true;
            }
            else
                _isStartingFileItemDrag = true;
            _previousMousePosition = e.GetPosition(this);
        }

        private void HandleFileItemMouseMove(object sender, MouseEventArgs e)
        {
            if (PictureListBox.SelectedItem != null &&
                (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
            {
                if (_isStartingFileItemDrag && (e.GetPosition(this) - _previousMousePosition).Length > 10)
                {
                    var files = _viewModel.GetSelectedItems().Select(i => i.FullPath).ToArray();
                    var data = new DataObject(DataFormats.FileDrop, files);
                    data.SetData(DataFormats.Text, files[0]);
                    DragDrop.DoDragDrop(this, data, DragDropEffects.All);
                    e.Handled = true;
                    _isStartingFileItemDrag = false;
                }
            }
            else
                _isStartingFileItemDrag = false;
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
                InitializePreviewRenderTransform(false);
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
                ZoomedPreviewImage.RenderTransform = null;
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
                _isDraggingPreview = _viewModel.PreviewZoom != 0;
                e.Handled = true;
            }
        }

        private void HandlePreviewImageMouseMove(object sender, MouseEventArgs e)
        {
            if ((e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) &&
                _isDraggingPreview && ZoomedPreviewImage.RenderTransform is MatrixTransform transform)
            {
                e.Handled = true;
                var pt = e.GetPosition(this);
                if (pt.Equals(_previousMousePosition))
                    return;
                var tx = transform.Matrix.OffsetX + pt.X - _previousMousePosition.X;
                var ty = transform.Matrix.OffsetY + pt.Y - _previousMousePosition.Y;
                if (tx > 0)
                    tx = transform.Matrix.OffsetX > 0 ? transform.Matrix.OffsetX : 0;
                if (ty > 0)
                    ty = transform.Matrix.OffsetY > 0 ? transform.Matrix.OffsetY : 0;
                ZoomedPreviewImage.RenderTransform = new MatrixTransform(
                    transform.Matrix.M11, transform.Matrix.M12,
                    transform.Matrix.M21, transform.Matrix.M22,
                    tx, ty);
                _previousMousePosition = pt;
            }
            else
                _isDraggingPreview = false;
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
                InitializePreviewRenderTransform(true);
            }
        }

        private void InitializePreviewRenderTransform(bool forceReset)
        {
            if (_viewModel.PreviewPictureSource is null || _viewModel.PreviewZoom == 0)
                return;
            var screenDpi = VisualTreeHelper.GetDpi(this);
            var zoom = _viewModel.PreviewZoom;
            var sx = _viewModel.PreviewPictureSource.DpiX / screenDpi.PixelsPerInchX * zoom;
            var sy = _viewModel.PreviewPictureSource.DpiY / screenDpi.PixelsPerInchY * zoom;
            if (!forceReset && ZoomedPreviewImage.RenderTransform is MatrixTransform m && 
                m.Matrix.M11 == sx && m.Matrix.M22 == sy && m.Matrix.OffsetX <= 0 && m.Matrix.OffsetY <= 0)
                return;
            var tx = IntMath.Round(ZoomedPreviewCanvas.ActualWidth - _viewModel.PreviewPictureSource.PixelWidth * zoom / screenDpi.PixelsPerInchX * 96) / 2;
            var ty = IntMath.Round(ZoomedPreviewCanvas.ActualHeight - _viewModel.PreviewPictureSource.PixelHeight * zoom / screenDpi.PixelsPerInchY * 96) / 2;
            ZoomedPreviewImage.RenderTransform = new MatrixTransform(
                sx, 0,
                0, sy,
                tx, ty);
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
        }
    }
}
