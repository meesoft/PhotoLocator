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

        public void InitializePreviewRenderTransform(bool forceReset)
        {
            if (_viewModel.PreviewPictureSource is null)
                return;
            var screenDpi = VisualTreeHelper.GetDpi(_zoomedPreviewImage);
            var zoom = _viewModel.PreviewZoom;
            var sx = _viewModel.PreviewPictureSource.DpiX / screenDpi.PixelsPerInchX * zoom;
            var sy = _viewModel.PreviewPictureSource.DpiY / screenDpi.PixelsPerInchY * zoom;
            var tx = CalcCenterTranslation(_previewCanvas.ActualWidth, _viewModel.PreviewPictureSource.PixelWidth, zoom, screenDpi.PixelsPerInchX);
            var ty = CalcCenterTranslation(_previewCanvas.ActualHeight, _viewModel.PreviewPictureSource.PixelHeight, zoom, screenDpi.PixelsPerInchY);
            if (!forceReset && _zoomedPreviewImage.RenderTransform is MatrixTransform m &&
                m.Matrix.M11 == sx && m.Matrix.M22 == sy && m.Matrix.OffsetX <= 0 && m.Matrix.OffsetY <= 0 && tx <= 0 && ty <= 0)
                return;
            _zoomedPreviewImage.RenderTransform = new MatrixTransform(
                sx, 0,
                0, sy,
                tx, ty);
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
