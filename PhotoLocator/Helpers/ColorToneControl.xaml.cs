using PhotoLocator.BitmapOperations;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    /// <summary>
    /// Interaction logic for ColorToneControl.xaml
    /// </summary>
    partial class ColorToneControl : UserControl
    {
        const int MaxClickDistanceSqr = 20;

        double _centerXY;
        int _highlightTone = -1;
        bool _isToneHit;
        BitmapSource? _colorBitmap;
        LocalContrastViewModel _viewModel = null!;

        public ColorToneControl()
        {
            InitializeComponent();
            DataContextChanged += HandleDataContextChanged;
        }

        private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            _viewModel = (LocalContrastViewModel)DataContext;
            if (_viewModel is not null)
                _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }

        private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(_viewModel.ActiveToneIndex)
                || e.PropertyName == nameof(_viewModel.HueAdjust)
                || e.PropertyName == nameof(_viewModel.SaturationAdjust)
                || e.PropertyName == nameof(_viewModel.IntensityAdjust)
                || e.PropertyName == nameof(_viewModel.ToneRotation))
                UpdateToneControlImage();
        }

        ColorToneAdjustOperation.ToneAdjustment[] ToneAdjustments => _viewModel.ToneAdjustments;

        int ActiveToneIndex
        {
            get => _viewModel.ActiveToneIndex;
            set => _viewModel.ActiveToneIndex = value;
        }

        static double SqrDistance(in Point p1, in Point p2) => RealMath.Sqr(p1.X - p2.X) + RealMath.Sqr(p1.Y - p2.Y);

        Point HS2XY(double h, double s)
        {
            var a1 = h * (2 * Math.PI) - Math.PI;
            var s1 = s * _centerXY;
            return new Point(
                _centerXY + Math.Cos(a1) * s1,
                _centerXY + Math.Sin(a1) * s1);
        }

        bool XY2HS(double x, double y, out float h, out float s)
        {
            var p = new Vector2d(x - _centerXY, y - _centerXY);
            s = (float)(p.Length() / _centerXY);
            if (s > 1)
            {
                h = float.NaN;
                return false;
            }
            h = (float)((p.Angle() + Math.PI) / (2 * Math.PI));
            return true;
        }

        private void UpdateColorBitmap()
        {
            if (!(ActualWidth > 1) || _colorBitmap is not null && _colorBitmap.PixelHeight == IntMath.Round(ActualWidth))
                return;
            var dpi = VisualTreeHelper.GetDpi(this);
            var image = new FloatBitmap(IntMath.Round(ActualWidth * dpi.DpiScaleX), IntMath.Round(ActualWidth * dpi.DpiScaleY), 4);
            _centerXY = (image.Width - 1) * 0.5;
            Parallel.For(0, image.Height, y =>
            {
                unsafe
                {
                    fixed (float* row = &image.Elements[y, 0])
                    {
                        float* pix = row;
                        for (int x = 0; x < image.Width; x++)
                        {
                            if (XY2HS(x, y, out float h, out float s))
                            {
                                ColorToneAdjustOperation.ColorTransformHSI2RGB(h, s, 0.5f, out pix[2], out pix[1], out pix[0]);
                                pix[3] = 255;
                            }
                            pix += 4;
                        }
                    }
                }
            });
            _centerXY = (ActualWidth - 1) * 0.5;
            _colorBitmap = image.ToBitmapSource(dpi.PixelsPerInchX, dpi.PixelsPerInchY, 1, PixelFormats.Bgra32);
            ColorWheel.Source = _colorBitmap;
        }

        void UpdateToneControlImage()
        {
            const double InactivePenThickness = 1.2;
            const double ActivePenThickness = 2;

            if (!(ActualWidth > 1) || _viewModel is null)
                return;
            var drawings = new DrawingGroup();
            {
                var drawing = new GeometryDrawing(Brushes.Black, new Pen(Brushes.Black, 0), // Invisible diagonal to define the size of the drawing
                    new LineGeometry(new Point(0, 0), new Point(ActualWidth, ActualWidth)));
                drawings.Children.Add(drawing);
            }
            var rotation = _viewModel.ToneRotation;
            for (int i = 0; i < ToneAdjustments.Length; i++)
            {
                var p1 = HS2XY(ToneAdjustments[i].ToneHue + rotation, 0.5);
                var p2 = HS2XY(ToneAdjustments[i].ToneHue + rotation + ToneAdjustments[i].AdjustHue, 0.5 * ToneAdjustments[i].AdjustSaturation);

                var group = new GeometryGroup();
                group.Children.Add(new EllipseGeometry(p1, 2, 2));
                group.Children.Add(new EllipseGeometry(p2, 2, 2));
                group.Children.Add(new LineGeometry(p1, p2));

                double thickness;
                if (_highlightTone >= 0)
                    thickness = i == _highlightTone ? ActivePenThickness : InactivePenThickness;
                else
                    thickness = i == ActiveToneIndex ? ActivePenThickness : InactivePenThickness;
                var drawing = new GeometryDrawing(Brushes.Black, new Pen(Brushes.Black, thickness), group);
                drawings.Children.Add(drawing);
            }
            drawings.ClipGeometry = new RectangleGeometry(new Rect(new Size(ActualWidth, ActualWidth)));
            var image = new DrawingImage(drawings);
            image.Freeze();
            AdjustmentImage.Source = image;
        }

        private void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _highlightTone >= 0)
            {
                ActiveToneIndex = _highlightTone;
                _isToneHit = true;
            }
            else
                _isToneHit = false;
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            var pt = e.GetPosition(AdjustmentImage);
            var rotation = _viewModel.ToneRotation;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_isToneHit && XY2HS(pt.X, pt.Y, out float h, out float s))
                {
                    h -= (float)rotation + ToneAdjustments[ActiveToneIndex].ToneHue;
                    if (h > 0.5f)
                        h -= 1;
                    else if (h < -0.5f)
                        h += 1;
                    _viewModel.HueAdjust = h;
                    _viewModel.SaturationAdjust = s * 2f;
                    UpdateToneControlImage();
                }
            }
            else
            {
                double bestDistance = MaxClickDistanceSqr;
                int prevHighlightTone = _highlightTone;
                _highlightTone = -1;
                for (int i = 0; i < ToneAdjustments.Length; i++)
                {
                    var p1 = HS2XY(ToneAdjustments[i].ToneHue + rotation, 0.5);
                    var p2 = HS2XY(ToneAdjustments[i].ToneHue + rotation + ToneAdjustments[i].AdjustHue, 0.5 * ToneAdjustments[i].AdjustSaturation);
                    var distance = SqrDistance(p1, pt);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        _highlightTone = i;
                    }
                    distance = SqrDistance(p2, pt);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        _highlightTone = i;
                    }
                }
                if (_highlightTone != prevHighlightTone)
                    UpdateToneControlImage();
            }
        }

        private void HandleSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateColorBitmap();
            UpdateToneControlImage();
        }
    }
}
