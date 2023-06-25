using MapControl;
using PhotoLocator.Helpers;
using PhotoLocator.MapDisplay;
using PhotoLocator.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for SlideShowWindow.xaml
    /// </summary>
    public partial class SlideShowWindow : Window, INotifyPropertyChanged
    {
        readonly IList<PictureItemViewModel> _pictures;
        readonly bool _showMetadataInSlideShow;
        readonly DispatcherTimer _timer;
        private TouchPoint? _touchStart;

        public SlideShowWindow(IList<PictureItemViewModel> pictures, PictureItemViewModel selectedPicture, int slideShowInterval, 
            BitmapScalingMode bitmapScalingMode, bool showMetadataInSlideShow, string? selectedMapLayerName)
        {
            _pictures = pictures;
            SelectedPicture = selectedPicture;
            _showMetadataInSlideShow = showMetadataInSlideShow;
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(slideShowInterval), DispatcherPriority.Normal, HandleTimerEvent, Dispatcher);
            BitmapScalingMode = bitmapScalingMode;
            InitializeComponent();
            DataContext = this;
            Map.map.TargetZoomLevel = 7;
            Map.mapLayersMenuButton.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => Equals(item.Header, selectedMapLayerName))?.
                RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Map.DataContext = this;
            PictureIndex = Math.Max(0, pictures.IndexOf(selectedPicture));
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

        public BitmapScalingMode BitmapScalingMode { get; }

        public ImageSource? PictureSource { get => _pictureSource; set => SetProperty(ref _pictureSource, value); }
        private ImageSource? _pictureSource;

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
            SelectedPicture = _pictures[PictureIndex];

            if (SelectedPicture.IsVideo)
            {
                MediaPlayer.Source = new Uri(SelectedPicture.FullPath);
                MediaPlayer.Visibility = Visibility.Visible;
                PictureSource = null;
            }
            else
            {
                PictureSource = SelectedPicture.LoadPreview(System.Threading.CancellationToken.None);
                MediaPlayer.Visibility = Visibility.Collapsed;
                MediaPlayer.Source = null;
                _timer.Start();
            }

            var name = Path.GetFileNameWithoutExtension(SelectedPicture.Name)!;
            var i = name.IndexOf('[', StringComparison.Ordinal);
            if (i > 2)
                name = name[..i].TrimEnd();
            if (_showMetadataInSlideShow)
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
    }
}
