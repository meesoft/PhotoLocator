using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Controls
{
    public interface ICropControl
    {
        Rect CropRectangle { get; }
    }

    /// <summary> Interaction logic for CropControl.xaml </summary>
    public partial class CropControl : UserControl, INotifyPropertyChanged, ICropControl
    {
        int _imageWidth, _imageHeight;
        Point _previousMousePosition;
        double _widthHeightRatio;
        string? _mouseOperation;

        public CropControl()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this))
                Reset(null, 500, 0);
        }

        public void Reset(BitmapSource? image, double imageScale, double cropWidthHeightRatio)
        {
            double pixelMean;
            if (image is null)
            {
                _imageWidth = _imageHeight = 1;
                pixelMean = 0;
            }
            else
            {
                _imageWidth = image.PixelWidth;
                _imageHeight = image.PixelHeight;
                pixelMean = new FloatBitmap(image, 1).Mean();
            }
            CropBorderColor = new SolidColorBrush(pixelMean > 0.2 ? Color.FromArgb(128, 0, 0, 0) : Color.FromArgb(64, 255, 255, 255));
            Width = _imageWidth * imageScale;
            Height = _imageHeight * imageScale;
            var imageWidthHeightRatio = (double)_imageWidth / _imageHeight;
            if (cropWidthHeightRatio > 0 && Math.Abs(cropWidthHeightRatio - imageWidthHeightRatio) > 0.01)
            {
                _widthHeightRatio = cropWidthHeightRatio;
                var cropScale = Math.Min(Width / cropWidthHeightRatio, Height);
                var cropRegionWidth = cropWidthHeightRatio * cropScale;
                CropLeftOffset = new((Width - cropRegionWidth) / 2, GridUnitType.Star);
                CropWidth = new(cropRegionWidth, GridUnitType.Star);
                CropRightOffset = CropLeftOffset;
                var cropRegionHeight = cropScale;
                CropTopOffset = new((Height - cropRegionHeight) / 2, GridUnitType.Star);
                CropHeight = new(cropRegionHeight, GridUnitType.Star);
                CropBottomOffset = CropTopOffset;
            }
            else
            {
                _widthHeightRatio = imageWidthHeightRatio;
                CropLeftOffset = new(0.05, GridUnitType.Star);
                CropWidth = new(0.9, GridUnitType.Star);
                CropRightOffset = new(0.05, GridUnitType.Star);
                CropTopOffset = new(0.05, GridUnitType.Star);
                CropHeight = new(0.9, GridUnitType.Star);
                CropBottomOffset = new(0.05, GridUnitType.Star);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RatioText)));
            return true;
        }

        public SolidColorBrush? CropBorderColor { get => field; private set => SetProperty(ref field, value); }

        public GridLength CropLeftOffset { get => field; set => SetProperty(ref field, value); }

        public GridLength CropWidth { get => field; set => SetProperty(ref field, value); }

        public GridLength CropRightOffset { get => field; set => SetProperty(ref field, value); }

        public GridLength CropTopOffset { get => field; set => SetProperty(ref field, value); }

        public GridLength CropHeight { get => field; set => SetProperty(ref field, value); }
        
        public GridLength CropBottomOffset { get => field; set => SetProperty(ref field, value); }

        public Rect CropRectangle
        {
            get
            {
                var horizontalSum = CropLeftOffset.Value + CropWidth.Value + CropRightOffset.Value;
                var verticalSum = CropTopOffset.Value + CropHeight.Value + CropBottomOffset.Value;
                return new Rect(
                    CropLeftOffset.Value / horizontalSum * _imageWidth,
                    CropTopOffset.Value / verticalSum * _imageHeight,
                    CropWidth.Value / horizontalSum * _imageWidth,
                    CropHeight.Value / verticalSum * _imageHeight);
            }
        }

        public string? RatioText
        {
            get
            {
                if (_imageWidth == 0 || _imageHeight == 0)
                    return null;
                var rect = CropRectangle;
                return $"{rect.Width:F0}x{rect.Height:F0} {rect.Width / rect.Height:F2}";
            }
        }

        private void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            _mouseOperation = (e.OriginalSource as FrameworkElement)?.Tag as string;
            if (_mouseOperation is null)
                return;
            _previousMousePosition = e.GetPosition(this);
            var rect = CropRectangle;
            if (rect.Width > 1)
                _widthHeightRatio = rect.Width / rect.Height;
            CaptureMouse();
            e.Handled = true;
        }

        private void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _mouseOperation == null)
                return;
            ReleaseMouseCapture();
            _mouseOperation = null;
            e.Handled = true;
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _mouseOperation is null)
                return;

            var mousePosition = e.GetPosition(this);

            var horizontalSum = CropLeftOffset.Value + CropWidth.Value + CropRightOffset.Value;
            var verticalSum = CropTopOffset.Value + CropHeight.Value + CropBottomOffset.Value;
            var dx = (mousePosition.X - _previousMousePosition.X) / ActualWidth * horizontalSum;

            void DragLeft()
            {
                dx = RealMath.Clamp(dx, -CropLeftOffset.Value, CropWidth.Value);
                CropLeftOffset = new GridLength(CropLeftOffset.Value + dx, CropLeftOffset.GridUnitType);
                CropWidth = new GridLength(CropWidth.Value - dx, CropWidth.GridUnitType);
            }

            void DragRight()
            {
                dx = RealMath.Clamp(dx, -CropWidth.Value, CropRightOffset.Value);
                CropRightOffset = new GridLength(CropRightOffset.Value - dx, CropRightOffset.GridUnitType);
                CropWidth = new GridLength(CropWidth.Value + dx, CropWidth.GridUnitType);
            }

            double dyFromWidth()
            {
                var newHeightInImage = CropWidth.Value / horizontalSum * _imageWidth / _widthHeightRatio;
                var newHeightInGrid = newHeightInImage / _imageHeight * verticalSum;
                return CropHeight.Value - newHeightInGrid;
            }

            void DragTop()
            {
                var dy = RealMath.Clamp(dyFromWidth(), -CropTopOffset.Value, CropHeight.Value);
                CropTopOffset = new GridLength(CropTopOffset.Value + dy, CropTopOffset.GridUnitType);
                CropHeight = new GridLength(CropHeight.Value - dy, CropHeight.GridUnitType);
            }

            void DragBottom()
            {
                var dy = RealMath.Clamp(dyFromWidth(), -CropBottomOffset.Value, CropHeight.Value);
                CropBottomOffset = new GridLength(CropBottomOffset.Value + dy, CropBottomOffset.GridUnitType);
                CropHeight = new GridLength(CropHeight.Value - dy, CropHeight.GridUnitType);
            }

            if (_mouseOperation == "Move")
            {
                dx = RealMath.Clamp(dx, -CropLeftOffset.Value, CropRightOffset.Value);
                CropLeftOffset = new GridLength(CropLeftOffset.Value + dx, CropLeftOffset.GridUnitType);
                CropRightOffset = new GridLength(CropRightOffset.Value - dx, CropRightOffset.GridUnitType);

                var dy = (mousePosition.Y - _previousMousePosition.Y) / ActualHeight * verticalSum;
                dy = RealMath.Clamp(dy, -CropTopOffset.Value, CropBottomOffset.Value);
                CropTopOffset = new GridLength(CropTopOffset.Value + dy, CropTopOffset.GridUnitType);
                CropBottomOffset = new GridLength(CropBottomOffset.Value - dy, CropBottomOffset.GridUnitType);
            }
            else if (_mouseOperation == "TopLeft")
            {
                DragLeft();
                DragTop();
            }
            else if (_mouseOperation == "TopRight")
            {
                DragRight();
                DragTop();
            }
            else if (_mouseOperation == "BottomLeft")
            {
                DragLeft();
                DragBottom();
            }
            else if (_mouseOperation == "BottomRight")
            {
                DragRight();
                DragBottom();
            }

            _previousMousePosition = mousePosition;
            e.Handled = true;
        }
    }
}
