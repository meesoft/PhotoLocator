using MapControl;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Metadata;
using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// <summary>
    /// Interaction logic for SlideShowWindow.xaml
    /// </summary>
    public sealed partial class SlideShowWindow : Window, INotifyPropertyChanged, IDisposable
    {
        readonly IList<PictureItemViewModel> _pictures;
        readonly DispatcherTimer _timer;
        private TouchPoint? _touchStart;
        private BitmapSource? _sourceImage;
        private CancellationTokenSource? _resamplerCancellation;

        public SlideShowWindow(IList<PictureItemViewModel> pictures, PictureItemViewModel selectedPicture, 
            string? selectedMapLayerName, ISettings settings)
        {
            _pictures = pictures;
            SelectedPicture = selectedPicture;
            Settings = settings;
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(settings.SlideShowInterval), DispatcherPriority.Normal, HandleTimerEvent, Dispatcher);
            InitializeComponent();
            DataContext = this;
            Map.map.TargetZoomLevel = 7;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedMapLayerName))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Map.DataContext = this;
            PictureIndex = Math.Max(0, pictures.IndexOf(selectedPicture));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public ISettings Settings { get; }

        public static Visibility MapToolsVisibility => Visibility.Hidden;

        public Location? MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }
        private Location? _mapCenter;
        
        public ObservableCollection<PointItem> Points { get; } = new ObservableCollection<PointItem>();

        public bool IsMapVisible { get => _isMapVisible; set => SetProperty(ref _isMapVisible, value); }
        private bool _isMapVisible;

        public ImageSource? PictureSource { get => _pictureSource; set => SetProperty(ref _pictureSource, value); }
        private ImageSource? _pictureSource;

        public ImageSource? ResampledPictureSource { get => _resampledPictureSource; set => SetProperty(ref _resampledPictureSource, value); }
        private ImageSource? _resampledPictureSource;

        public string? PictureTitle { get => _pictureTitle; set => SetProperty(ref _pictureTitle, value); }
        private string? _pictureTitle;

        public PictureItemViewModel SelectedPicture { get; private set; }

        public int PictureIndex 
        { 
            get => _pictureIndex;
            set
            {
                if (SetProperty(ref _pictureIndex, Math.Max(0, Math.Min(_pictures.Count - 1, value))))
                    UpdatePicture();
            }
        }
        int _pictureIndex = -1;

        private void UpdatePicture()
        {
            _timer.Stop();
            _resamplerCancellation?.Cancel();
            SelectedPicture = _pictures[PictureIndex];

            if (SelectedPicture.IsVideo)
            {
                MediaPlayer.Source = new Uri(SelectedPicture.FullPath);
                MediaPlayer.Visibility = Visibility.Visible;
                PictureSource = null;
                ResampledPictureSource = null;
                _sourceImage = null;
            }
            else
            {
                _timer.Start();
                _sourceImage = SelectedPicture.LoadPreview(default);
                UpdateResampledImage();
            }

            var name = Path.GetFileNameWithoutExtension(SelectedPicture.Name)!;
            var i = name.IndexOf('[', StringComparison.Ordinal);
            if (i > 2)
                name = name[..i].TrimEnd();
            if (Settings.ShowMetadataInSlideShow)
            {
                var metadata = ExifHandler.GetMetataString(SelectedPicture.FullPath);
                if (!string.IsNullOrEmpty(metadata))
                    name = name + " [" + metadata + "]";
            }
            PictureTitle = name;

            if (SelectedPicture.GeoTag is null)
                IsMapVisible = false;
            else
            {
                MapCenter = SelectedPicture.GeoTag;
                Points.Clear();
                Points.Add(new PointItem { Location = SelectedPicture.GeoTag });
                IsMapVisible = true;
            }

            WinAPI.KeepDisplayAlive();
        }

        private async void UpdateResampledImage()
        {
            _resamplerCancellation?.Cancel();
            _resamplerCancellation?.Dispose();
            _resamplerCancellation = null;
            if (_sourceImage is null || ScreenGrid.ActualWidth < 1 || ScreenGrid.ActualHeight < 1)
            {
                PictureSource = null;
                ResampledPictureSource = null;
            }
            else
            {
                var screenDpi = VisualTreeHelper.GetDpi(this);
                var maxWidth = ScreenGrid.ActualWidth * screenDpi.DpiScaleX;
                var MaxHeight = ScreenGrid.ActualHeight * screenDpi.DpiScaleY;
                var scale = Math.Min(maxWidth / _sourceImage.PixelWidth, MaxHeight / _sourceImage.PixelHeight);
                var resizeOperation = new LanczosResizeOperation();
                _resamplerCancellation = new CancellationTokenSource();
                var resampled = await Task.Run(() => resizeOperation.Apply(_sourceImage,
                    (int)(_sourceImage.PixelWidth * scale), (int)(_sourceImage.PixelHeight * scale),
                    screenDpi.PixelsPerInchX, screenDpi.PixelsPerInchY, _resamplerCancellation.Token), _resamplerCancellation.Token);
                if (resampled is null)
                {
                    PictureSource = _sourceImage;
                    ResampledPictureSource = null;
                }
                else
                {
                    ResampledPictureSource = resampled;
                    PictureSource = null;
                }
            }
            MediaPlayer.Visibility = Visibility.Collapsed;
            MediaPlayer.Source = null;
        }

        private void HandleTimerEvent(object? sender, EventArgs e)
        {
            PictureIndex = (PictureIndex + 1) % _pictures.Count;
            UpdatePicture();
        }

        private void HandlePreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
            else if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.PageUp)
                PictureIndex--;
            else if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.PageDown)
                PictureIndex++;
            else if (e.Key == Key.Home)
                PictureIndex = 0;
            else if (e.Key == Key.End)
                PictureIndex = _pictures.Count - 1;
        }

        private void HandleTouchDown(object sender, TouchEventArgs e)
        {
            _touchStart = e.GetTouchPoint(this);
        }

        private void HandleTouchMove(object sender, TouchEventArgs e)
        {
            const int MinDragDist = 120;
            if (_touchStart is null)
                return;
            var pt = e.GetTouchPoint(this);
            if (pt.Position.X < _touchStart.Position.X - MinDragDist || 
                pt.Position.Y < _touchStart.Position.Y - MinDragDist)
            {
                PictureIndex++;
                _touchStart = null;
            }
            else if (pt.Position.X > _touchStart.Position.X + MinDragDist || 
                     pt.Position.Y > _touchStart.Position.Y + MinDragDist)
            {
                PictureIndex--;
                _touchStart = null;
            }
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                PictureIndex--;
                e.Handled = true;
            }
            else if (e.Delta < 0)
            {
                PictureIndex++;
                e.Handled = true;
            }
        }

        private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_sourceImage is not null)
                UpdateResampledImage();
        }

        public void Dispose()
        {
            _resamplerCancellation?.Cancel();
            _resamplerCancellation?.Dispose();
            _resamplerCancellation = null;
        }
    }
}
