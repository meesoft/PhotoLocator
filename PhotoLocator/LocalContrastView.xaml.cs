using PhotoLocator.Helpers;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PhotoLocator.BitmapOperations;
using System.Numerics;
using System.Threading.Tasks;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for LocalContrastView.xaml
    /// </summary>
    public partial class LocalContrastView : Window
    {
        LocalContrastViewModel _viewModel = null!;
        ImageZoomPreviewViewHelper _zoomPreviewViewHelper = null!;

        public LocalContrastView()
        {
            InitializeComponent();
            DataContextChanged += HandleDataContextChanged;
        }

        private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
            _viewModel = (LocalContrastViewModel)DataContext;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
                _zoomPreviewViewHelper = new ImageZoomPreviewViewHelper(PreviewCanvas, ZoomedPreviewImage, _viewModel);
                UpdatePreviewZoom();
            }
        }

        private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_viewModel.PreviewZoom))
                UpdatePreviewZoom();
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomInCommand.Execute(null);
            }
            else if (e.Delta < 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomOutCommand.Execute(null);
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
                FullPreviewImage.Visibility = Visibility.Visible;
                ZoomedPreviewImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                ZoomedPreviewImage.Visibility = Visibility.Visible;
                UpdateLayout();
                _zoomPreviewViewHelper.InitializePreviewRenderTransform(true);
                FullPreviewImage.Visibility = Visibility.Collapsed;
            }
        }

        private void HandleOriginalButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.PreviewPictureSource = _viewModel.SourceBitmap;
            _viewModel.ShowSourceHistogram();
        }

        private async void HandleOriginalButtonMouseUp(object sender, MouseButtonEventArgs e)
        {
            await _viewModel.UpdatePreviewAsync();
        }

        private async void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            if (DialogResult is not null)
                return;
            await _viewModel.FinishPreviewAsync();
            DialogResult ??= true;
        }

        private async void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1000);
            PreviewGrid.PreviewMouseMove += HandlePreviewMouseMove;
        }

        private void HandlePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                _viewModel.ColorUnderCursor = null;
                return;
            }

            var pos = e.GetPosition(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            var windowX = IntMath.Round(pos.X * dpi.PixelsPerInchX / 96);
            var windowY = IntMath.Round(pos.Y * dpi.PixelsPerInchY / 96);

            // Read pixel from the current window DC
            var hwnd = new WindowInteropHelper(this).Handle;
            var hdc = WinAPI.GetDC(hwnd);
            try
            {
                var pixel = WinAPI.GetPixel(hdc, windowX, windowY);
                var r = (pixel & 0x000000FF);
                var g = (pixel & 0x0000FF00) >> 8;
                var b = (pixel & 0x00FF0000) >> 16;
                
                if (r == 0 && g == 0 && b == 0)
                    _viewModel.ColorUnderCursor = null;
                else
                    _viewModel.ColorUnderCursor = new Vector3(
                        (float)Math.Pow(r / 255.0, FloatBitmap.DefaultMonitorGamma), 
                        (float)Math.Pow(g / 255.0, FloatBitmap.DefaultMonitorGamma), 
                        (float)Math.Pow(b / 255.0, FloatBitmap.DefaultMonitorGamma));
            }
            finally
            {
                _ = WinAPI.ReleaseDC(hwnd, hdc);
            }
        }

        private void HandlePreviewMouseLeave(object sender, MouseEventArgs e)
        {
            _viewModel?.ColorUnderCursor = null;
        }
    }
}
