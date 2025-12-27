using MapControl;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Gps;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
        readonly IList<PictureItemViewModel> _slideShowItems;
        readonly DispatcherTimer _timer;
        List<PictureItemViewModel> _pictures;
        TouchPoint? _touchStart;
        BitmapSource? _sourceImage;
        CancellationTokenSource? _resamplerCancellation;

        public SlideShowWindow(IList<PictureItemViewModel> slideShowItems, PictureItemViewModel? selectedPicture, 
            string? selectedMapLayerName, ISettings settings)
        {
            _slideShowItems = slideShowItems;
            Settings = settings;
            UpdateFolders();
            SelectedPicture = selectedPicture ?? _pictures.FirstOrDefault() ?? throw new UserMessageException("The folder does not have any pictures to show");
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(settings.SlideShowInterval), DispatcherPriority.Normal, HandleTimerEvent, Dispatcher);
            InitializeComponent();
            DataContext = this;
            Map.map.TargetZoomLevel = 7;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedMapLayerName))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Map.DataContext = this;
            PictureIndex = Math.Max(0, _pictures.IndexOf(SelectedPicture));
        }

        [MemberNotNull(nameof(_pictures))]
        private void UpdateFolders()
        {
            _pictures = _slideShowItems.Where(item => item.IsFile).ToList();
            HashSet<string>? extensions = null;
            foreach (var folder in _slideShowItems.Where(item => item.IsDirectory))
            {
                extensions ??= Settings.CleanPhotoFileExtensions().ToHashSet();
                foreach (var folderPicture in Directory.EnumerateFiles(folder.FullPath).Where(fn => extensions.Contains(Path.GetExtension(fn).ToLowerInvariant())))
                    _pictures.Add(new PictureItemViewModel(folderPicture, false, null, Settings));
            }
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
            get;
            set => SetProperty(ref field, value);
        }

        public ObservableCollection<PointItem> Points { get; } = [];

        public ObservableCollection<PointItem> Pushpins { get; } = [];

        public ObservableCollection<GpsTrace> Polylines { get; } = [];

        public bool IsMapVisible { get; set => SetProperty(ref field, value); }

        public ImageSource? PictureSource { get; set => SetProperty(ref field, value); }

        public ImageSource? ResampledPictureSource { get; set => SetProperty(ref field, value); }

        public string? PictureTitle { get; set => SetProperty(ref field, value); }

        public PictureItemViewModel SelectedPicture { get; private set; }

        public int PictureIndex 
        { 
            get;
            set
            {
                if (SetProperty(ref field, Math.Max(0, Math.Min(_pictures.Count - 1, value))))
                    UpdatePictureAsync().WithExceptionLogging();
            }
        } = -1;

        async Task UpdatePictureAsync()
        {
            if (_pictures.Count == 0)
                return;
            _timer.Stop();
            await (_resamplerCancellation?.CancelAsync() ?? Task.CompletedTask);
            SelectedPicture = _pictures[PictureIndex];
            if (SelectedPicture.ThumbnailImage is null)
                await SelectedPicture.LoadThumbnailAndMetadataAsync(default);

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
                if (!string.IsNullOrEmpty(SelectedPicture.MetadataString))
                    name = name + " [" + SelectedPicture.MetadataString + "]";
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

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            var screenDpi = VisualTreeHelper.GetDpi(this);
            WinAPI.SetCursorPos((int)((Left + Width) * screenDpi.DpiScaleX), (int)((Top + Height + 10) * screenDpi.DpiScaleY));
        }

        private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_sourceImage is not null)
                UpdateResampledImage();
        }

        private void HandleTimerEvent(object? sender, EventArgs e)
        {
            if (!IsVisible)
            {
                _timer.Stop();
                return;
            }
            if (PictureIndex + 1 >= _pictures.Count)
            {
                UpdateFolders();
                PictureIndex = 0;
            }
            else
                PictureIndex++;
        }

        private void HandlePreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Escape or Key.Q)
                Close();
            else if (e.Key is Key.Left or Key.Up or Key.PageUp)
                PictureIndex--;
            else if (e.Key is Key.Right or Key.Down or Key.PageDown)
                PictureIndex++;
            else if (e.Key == Key.Home)
                PictureIndex = 0;
            else if (e.Key == Key.End)
                PictureIndex = _pictures.Count - 1;
        }

        private void HandlePreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount >= 2)
            {
                Close();
                e.Handled = true;
            }
        }

        private void HandleTouchDown(object sender, TouchEventArgs e)
        {
            _touchStart = e.GetTouchPoint(this);
        }

        private void HandleTouchMove(object sender, TouchEventArgs e)
        {
            const int MinDragDistance = 120;
            if (_touchStart is null)
                return;
            var pt = e.GetTouchPoint(this);
            if (pt.Position.X < _touchStart.Position.X - MinDragDistance || 
                pt.Position.Y < _touchStart.Position.Y - MinDragDistance)
            {
                PictureIndex++;
                _touchStart = null;
            }
            else if (pt.Position.X > _touchStart.Position.X + MinDragDistance || 
                     pt.Position.Y > _touchStart.Position.Y + MinDragDistance)
            {
                PictureIndex--;
                _touchStart = null;
            }
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                PictureIndex--;
            else if (e.Delta < 0)
                PictureIndex++;
            e.Handled = true;
        }

        public void Dispose()
        {
            _resamplerCancellation?.Cancel();
            _resamplerCancellation?.Dispose();
            _resamplerCancellation = null;
            _timer.Stop();
            PictureSource = null;
            ResampledPictureSource = null;
            DataContext = null;
        }
    }
}
