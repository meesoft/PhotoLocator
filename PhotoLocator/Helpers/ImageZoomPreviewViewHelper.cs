using PhotoLocator.BitmapOperations;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoLocator.Helpers
{
    class ImageZoomPreviewViewHelper
    {
        readonly Canvas _previewCanvas;
        readonly Image _zoomedPreviewImage;
        readonly IImageZoomPreviewViewModel _viewModel;
        Task<RegistrationOperation>? _previousImageRegistration;
        Point _previousMousePosition;
        bool _isDraggingPreview;

        public ImageZoomPreviewViewHelper(Canvas previewCanvas, Image zoomedPreviewImage, IImageZoomPreviewViewModel viewModel)
        {
            _previewCanvas = previewCanvas;
            _zoomedPreviewImage = zoomedPreviewImage;
            _viewModel = viewModel;
            _zoomedPreviewImage.PreviewMouseDown += HandlePreviewImageMouseDown;
            _zoomedPreviewImage.PreviewMouseMove += HandlePreviewImageMouseMove;
        }

        public void InitializePreviewRenderTransform(bool forceReset, bool registerToPrevious = false)
        {
            if (_viewModel.PreviewPictureSource is null)
            {
                _previousImageRegistration?.ContinueWith(t => t.Result.Dispose(), TaskScheduler.Default);
                _previousImageRegistration = null;
                return;
            }
            var screenDpi = VisualTreeHelper.GetDpi(_zoomedPreviewImage);
            var zoom = _viewModel.PreviewZoom;
            var sx = _viewModel.PreviewPictureSource.DpiX / screenDpi.PixelsPerInchX * zoom;
            var sy = _viewModel.PreviewPictureSource.DpiY / screenDpi.PixelsPerInchY * zoom;
            var tx = CalcCenterTranslation(_previewCanvas.ActualWidth, _viewModel.PreviewPictureSource.PixelWidth, zoom, screenDpi.PixelsPerInchX);
            var ty = CalcCenterTranslation(_previewCanvas.ActualHeight, _viewModel.PreviewPictureSource.PixelHeight, zoom, screenDpi.PixelsPerInchY);

            var previousImageRegistration = _previousImageRegistration;
            if (registerToPrevious)
                _previousImageRegistration = Task.Run(() => new RegistrationOperation(_viewModel.PreviewPictureSource));
            else
                _previousImageRegistration = null;

            if (!forceReset && _zoomedPreviewImage.RenderTransform is MatrixTransform m &&
                m.Matrix.M11 == sx && m.Matrix.M22 == sy && m.Matrix.OffsetX <= 0 && m.Matrix.OffsetY <= 0 && tx <= 0 && ty <= 0) // Keep translation
            {
                if (registerToPrevious && previousImageRegistration is not null)
                {
                    try
                    {
                        var registration = previousImageRegistration.Result;
                        var translation = registration.GetTranslation(_viewModel.PreviewPictureSource);
                        _zoomedPreviewImage.RenderTransform = new MatrixTransform(
                            m.Matrix.M11, m.Matrix.M12,
                            m.Matrix.M21, m.Matrix.M22,
                            m.Matrix.OffsetX + translation.X * sx, m.Matrix.OffsetY + translation.Y * sy);
                        previousImageRegistration = null;
                        registration.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"Registration failed: {ex.Message}");
                    }
                }
            }
            else // Reset translation
            {
                _zoomedPreviewImage.RenderTransform = new MatrixTransform(
                    sx, 0,
                    0, sy,
                    tx, ty);
            }
            previousImageRegistration?.ContinueWith(t => t.Result.Dispose(), TaskScheduler.Default);
        }

        public static double CalcCenterTranslation(double canvasSizeIn96, int imageSize, int zoom, double screenDpi)
        {
            return IntMath.Round((canvasSizeIn96 - imageSize * zoom / screenDpi * 96) / 2) + 0.5;
        }

        private void HandlePreviewImageMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.PreviewZoom > 0 && e.ChangedButton is MouseButton.Left or MouseButton.Middle)
            {
                _previousMousePosition = e.GetPosition(_previewCanvas);
                _isDraggingPreview = true;
                e.Handled = true;
            }
        }

        private void HandlePreviewImageMouseMove(object sender, MouseEventArgs e)
        {
            if ((e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) &&
                _isDraggingPreview && _zoomedPreviewImage.RenderTransform is MatrixTransform transform)
            {
                e.Handled = true;
                var pt = e.GetPosition(_previewCanvas);
                if (pt.Equals(_previousMousePosition))
                    return;
                var tx = transform.Matrix.OffsetX + pt.X - _previousMousePosition.X;
                var ty = transform.Matrix.OffsetY + pt.Y - _previousMousePosition.Y;
                if (tx > 0)
                    tx = transform.Matrix.OffsetX > 0 ? transform.Matrix.OffsetX : 0;
                if (ty > 0)
                    ty = transform.Matrix.OffsetY > 0 ? transform.Matrix.OffsetY : 0;
                _zoomedPreviewImage.RenderTransform = new MatrixTransform(
                    transform.Matrix.M11, transform.Matrix.M12,
                    transform.Matrix.M21, transform.Matrix.M22,
                    tx, ty);
                _previousMousePosition = pt;
            }
            else
                _isDraggingPreview = false;
        }
    }
}
