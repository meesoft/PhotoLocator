using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Settings;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoLocator
{
    /// <summary> Interaction logic for MainWindow.xaml </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        readonly MainViewModel _viewModel;
        readonly ImageZoomPreviewViewHelper _zoomPreviewViewHelper;
        Point _previousMousePosition;
        bool _isStartingFileItemDrag;
        int _selectStartIndex;
        CancellationTokenSource? _resamplerCancellation;
        DispatcherTimer? _resamplerTimer;
        string[]? _draggedFiles;
        bool _isLogActive;

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
            _viewModel.Settings.PropertyChanged += HandleViewModelPropertyChanged;
            _zoomPreviewViewHelper = new ImageZoomPreviewViewHelper(PreviewCanvas, ZoomedPreviewImage, _viewModel);
            CropGrid.DataContext = CropGrid;
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            using var registrySettings = new RegistrySettings();
            _viewModel.Settings.AssignSettings(registrySettings);
            CheckToolPaths();

            _viewModel.PhotoFileExtensions = _viewModel.Settings.CleanPhotoFileExtensions();
            var leftColumnWidth = registrySettings.LeftColumnWidth;
            if (leftColumnWidth > 10 && leftColumnWidth < Width)
                LeftColumn.Width = new GridLength(leftColumnWidth);
            var selectedLayer = registrySettings.SelectedLayer;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedLayer))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Map.MapItemSelected += _viewModel.HandleMapItemSelected;
            _viewModel.SelectedViewModeItem = registrySettings.ViewMode switch
            {
                ViewMode.Preview => PreviewViewItem,
                ViewMode.Split => SplitViewItem,
                _ => MapViewItem
            };

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1]))
                Dispatcher.BeginInvoke(() =>
                {
                    _viewModel.SetFolderPathAsync(Path.GetDirectoryName(args[1]), args[1]).WithExceptionShowing();
                });
            else
            {
                var savedPhotoFolderPath = registrySettings.PhotoFolderPath;
                if (!Directory.Exists(savedPhotoFolderPath))
                    savedPhotoFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                Dispatcher.BeginInvoke(() => _viewModel.PhotoFolderPath = savedPhotoFolderPath);
            }

            if (registrySettings.FirstLaunch < 1)
            {
                MainViewModel.AboutCommand.Execute(null);
                registrySettings.FirstLaunch = 1;
            }
            PictureListBox.Focus();

            LogTextBox.SizeChanged += (s, e) => UpdateLogView();

            Task.Run(() => CleanupTileCache(MapView.TileCachePath)).WithExceptionLogging();
        }

        private void CheckToolPaths()
        {
            if (string.IsNullOrEmpty(_viewModel.Settings.ExifToolPath))
            {
                var exifToolPath = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "exiftool");
                if (Directory.Exists(exifToolPath))
                    _viewModel.Settings.ExifToolPath = Directory.EnumerateFiles(exifToolPath, "*.exe").FirstOrDefault();
            }
            if (string.IsNullOrEmpty(_viewModel.Settings.FFmpegPath))
            {
                var ffmpegPath = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "ffmpeg", "ffmpeg.exe");
                if (File.Exists(ffmpegPath))
                    _viewModel.Settings.FFmpegPath = ffmpegPath;
            }
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
            using var registrySettings = new RegistrySettings();
            registrySettings.AssignSettings(_viewModel.Settings);
            if (!string.IsNullOrEmpty(_viewModel.PhotoFolderPath))
                registrySettings.PhotoFolderPath = _viewModel.PhotoFolderPath;
            registrySettings.ViewMode = _viewModel.SelectedViewModeItem?.Tag as ViewMode? ?? ViewMode.Map;
            registrySettings.LeftColumnWidth = (int)LeftColumn.Width.Value;
            registrySettings.SelectedLayer = GetSelectedMapLayerName();
            _viewModel.Dispose();
        }

        private string? GetSelectedMapLayerName()
        {
            return Map.mapLayersMenuButton.ContextMenu.Items.Cast<MenuItem>().FirstOrDefault(i => i.IsChecked)?.Header as string;
        }

        private void FocusListBoxItem(object item)
        {
            var listBoxItem = (ListBoxItem)PictureListBox.ItemContainerGenerator.ContainerFromItem(item);
            listBoxItem.BringIntoView();
            listBoxItem.Focus();
        }

        private void HandlePictureListBoxPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton2 && _viewModel.SelectedItem != null && _viewModel.SelectedItem.IsDirectory)
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
            if (SelectItem(e.Text, PictureListBox.SelectedIndex + 1, _viewModel.Items.Count) ||
                SelectItem(e.Text, 0, PictureListBox.SelectedIndex))
                e.Handled = true;

            bool SelectItem(string text, int min, int max)
            {
                for (int i = min; i < max; i++)
                    if (_viewModel.Items[i].Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _viewModel.SelectIfNotNull(_viewModel.Items[i]);
                        return true;
                    }
                return false;
            }
        }

        private void HandlePictureListBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var selectedItem = _viewModel.SelectedItem;
            if (selectedItem is null)
                return;
            if (e.Key == Key.Space)
            {
                selectedItem.IsChecked = !selectedItem.IsChecked;
                e.Handled = true;
            }
            else if (e.Key == Key.Insert && !e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) && !e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
            {
                if (!(e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift)))
                    selectedItem.IsChecked = !selectedItem.IsChecked;
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
            else if (e.Key is Key.LeftShift or Key.RightShift)
            {
                _selectStartIndex = Math.Max(0, PictureListBox.SelectedIndex);
            }
            else if (e.Key == Key.Escape) // Prevent Escape from navigating to the first item
                e.Handled = true;
        }

        private void HandlePictureListBoxPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Home || e.Key == Key.End || e.Key == Key.PageDown || e.Key == Key.PageUp || 
                 e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right) && 
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

        private void HandleWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel.IsCropControlVisible)
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _viewModel.CropCommand.Execute(true);
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    _viewModel.IsCropControlVisible = false;
                }
            }
        }

        private void HandleFileItemMouseMove(object sender, MouseEventArgs e)
        {
            if (PictureListBox.SelectedItem != null &&
                (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
            {
                if (_isStartingFileItemDrag && (e.GetPosition(this) - _previousMousePosition).Length > 10)
                {
                    _draggedFiles = _viewModel.GetSelectedItems(false).Select(i => i.FullPath).ToArray();
                    var data = new DataObject(DataFormats.FileDrop, _draggedFiles);
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] droppedEntries && droppedEntries.Length > 0
                && !droppedEntries.Equals(_draggedFiles))
            {
                Dispatcher.BeginInvoke(() => _viewModel.HandleDroppedFiles(droppedEntries));
            }
        }

        private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.PreviewPictureSource))
            {
                if (_viewModel.PreviewZoom > 0)
                    _zoomPreviewViewHelper.InitializePreviewRenderTransform(false);
                else
                {
                    if (_viewModel.Settings.LanczosUpscaling || _viewModel.Settings.LanczosDownscaling)
                        UpdateResampledImage();
                }
            }
            else if (e.PropertyName is nameof(_viewModel.PreviewZoom) or nameof(_viewModel.Settings.ResamplingOptions))
                UpdatePreviewZoom();
            else if (e.PropertyName is nameof(_viewModel.IsCropControlVisible))
            {
                if (_viewModel.IsCropControlVisible)
                    ShowCropControl();
            }
        }

        private void UpdateLogView()
        {
            var isLogActive = LogTextBox.ActualHeight > 4;
            if (isLogActive == _isLogActive)
                return;
            _isLogActive = isLogActive;
            if (isLogActive)
            {
                LogTextBox.Text = string.Join("\n", Log.GetHistory());
                LogTextBox.ScrollToEnd();
                Log.EventAdded += HandleLogEventAdded;
            }
            else
            {
                Log.EventAdded -= HandleLogEventAdded;
                LogTextBox.Text = string.Empty;
            }
        }

        private void HandleLogEventAdded(string line)
        {
            Dispatcher.BeginInvoke(() =>
            { 
                LogTextBox.AppendText("\n" + line); 
                LogTextBox.ScrollToEnd();
            });
        }

        private void ShowCropControl()
        {
            var sourceImage = _viewModel.PreviewPictureSource;
            if (sourceImage is null)
                _viewModel.IsCropControlVisible = false;
            else
            {
                var imageScale = Math.Min(PreviewCanvas.ActualWidth / sourceImage.PixelWidth, PreviewCanvas.ActualHeight / sourceImage.PixelHeight);
                CropGrid.Reset(sourceImage.PixelWidth, sourceImage.PixelHeight, imageScale, _viewModel.Settings.CropWidthHeightRatio);
                _viewModel.CropControl = CropGrid;
            }
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

        private void HandlePreviewCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel.PreviewZoom == 0 && (_viewModel.Settings.LanczosUpscaling || _viewModel.Settings.LanczosDownscaling))
            {
                _resamplerTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.ApplicationIdle, (s, e) =>
                {
                    _resamplerTimer?.Stop();
                    UpdateResampledImage();
                }, Dispatcher);
                _resamplerTimer.Start();
            }
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel.IsCropControlVisible)
                return;
            if (e.Delta > 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomInCommand.Execute(null);
                else
                {
                    PictureListBox.SelectedIndex--;
                    PictureListBox.ScrollIntoView(PictureListBox.SelectedItem);
                }
            }
            else if (e.Delta < 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomOutCommand.Execute(null);
                else
                {
                    PictureListBox.SelectedIndex++;
                    PictureListBox.ScrollIntoView(PictureListBox.SelectedItem);
                }
            }
            e.Handled = true;
        }

        private void UpdatePreviewZoom()
        {
            ZoomToFitItem.IsChecked = _viewModel.PreviewZoom == 0;
            Zoom100Item.IsChecked = _viewModel.PreviewZoom == 1;
            Zoom200Item.IsChecked = _viewModel.PreviewZoom == 2;
            Zoom400Item.IsChecked = _viewModel.PreviewZoom == 4;
            if (_viewModel.PreviewZoom == 0)
            {
                if (_viewModel.Settings.LanczosUpscaling || _viewModel.Settings.LanczosDownscaling)
                {
                    ResampledPreviewImage.Visibility = Visibility.Visible;
                    UpdateLayout();
                    UpdateResampledImage();
                }
                else // WPF scaling
                {
                    FullPreviewImage.Visibility = Visibility.Visible;
                    ZoomedPreviewImage.Visibility = Visibility.Collapsed;
                    ResampledPreviewImage.Visibility = Visibility.Collapsed;
                    ResampledPreviewImage.Source = null;
                }
            }
            else
            {
                ZoomedPreviewImage.Visibility = Visibility.Visible;
                UpdateLayout();
                _zoomPreviewViewHelper.InitializePreviewRenderTransform(true);
                FullPreviewImage.Visibility = Visibility.Collapsed;
                ResampledPreviewImage.Visibility = Visibility.Collapsed;
                ResampledPreviewImage.Source = null;
            }
        }

        private async void UpdateResampledImage()
        {
            _resamplerCancellation?.Cancel();
            _resamplerCancellation?.Dispose();
            _resamplerCancellation = null;
            var sourceImage = _viewModel.PreviewPictureSource;
            if (sourceImage is null || PreviewCanvas.ActualWidth < 1 || PreviewCanvas.ActualHeight < 1 || _viewModel.PreviewZoom > 0)
            {
                ResampledPreviewImage.Source = null;
                return;
            }
            var screenDpi = VisualTreeHelper.GetDpi(this);
            var maxWidth = PreviewCanvas.ActualWidth * screenDpi.DpiScaleX;
            var MaxHeight = PreviewCanvas.ActualHeight * screenDpi.DpiScaleY;
            var scale = Math.Min(maxWidth / sourceImage.PixelWidth, MaxHeight / sourceImage.PixelHeight);
            BitmapSource? resampled = null;
            if (scale > 1 && _viewModel.Settings.LanczosUpscaling || scale < 1 && _viewModel.Settings.LanczosDownscaling)
            {
                var resizeOperation = new LanczosResizeOperation();
                _resamplerCancellation = new CancellationTokenSource();
                resampled = await Task.Run(() => resizeOperation.Apply(sourceImage,
                    (int)(sourceImage.PixelWidth * scale), (int)(sourceImage.PixelHeight * scale),
                    screenDpi.PixelsPerInchX, screenDpi.PixelsPerInchY,
                    _resamplerCancellation.Token), _resamplerCancellation.Token);
            }
            if (sourceImage == _viewModel.PreviewPictureSource)
            {
                ResampledPreviewImage.Source = resampled;
                if (resampled is null)
                    FullPreviewImage.Visibility = Visibility.Visible;
                else
                {
                    var tx = ImageZoomPreviewViewHelper.CalcCenterTranslation(PreviewCanvas.ActualWidth, resampled.PixelWidth, 1, screenDpi.PixelsPerInchX);
                    var ty = ImageZoomPreviewViewHelper.CalcCenterTranslation(PreviewCanvas.ActualHeight, resampled.PixelHeight, 1, screenDpi.PixelsPerInchY);
                    ResampledPreviewImage.RenderTransform = new MatrixTransform(
                        1, 0,
                        0, 1,
                        tx, ty);
                    FullPreviewImage.Visibility = Visibility.Collapsed;
                }
                ZoomedPreviewImage.Visibility = Visibility.Collapsed;
            }
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
            _resamplerCancellation?.Dispose();
            Log.EventAdded -= HandleLogEventAdded;
        }
    }
}
