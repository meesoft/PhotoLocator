using OpenCvSharp;
using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class RegistrationOperation : IDisposable
    {
        public enum Borders { Black, Mirror }

        public enum Reference { First, Previous }

        const int MinimumFeatureCount = 10;
        const int MinimumMatches = 10;

        readonly int _width;
        readonly int _height;
        readonly int _pixelSize;
        readonly Reference _reference;
        readonly Borders _borderHandling;
        readonly ROI? _roi;
        Mat _referenceGrayImage;
        Point2f[] _referenceFeatures;
        Mat? _previousTrans;
#if DEBUG
        //int _frameCount;
#endif
        public RegistrationOperation(byte[] pixels, int width, int height, int pixelSize, Reference reference, Borders borderHandling, ROI? roi = null)
        {
            _width = width;
            _height = height;
            _pixelSize = pixelSize;
            _reference = reference;
            _borderHandling = borderHandling;
            _referenceGrayImage = ConvertToGrayscale(pixels);
            _roi = roi;
            _referenceFeatures = FindFirstFeatures();
            //SaveAnnotated(_referenceGrayImage, _referenceFeatures.Select(p => new Point(p.X, p.Y)), @"c:\temp\0.jpg");
        }

        public RegistrationOperation(BitmapSource image, ROI? roi = null)
        {
            _width = image.PixelWidth;
            _height = image.PixelHeight;
            var pixelFormat = image.Format;
            if (pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32 || pixelFormat == PixelFormats.Cmyk32)
                _pixelSize = 4;
            else if (pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Bgr24)
                _pixelSize = 3;
            else if (pixelFormat == PixelFormats.Gray8)
                _pixelSize = 1;
            else
                throw new UserMessageException("Unsupported pixel format " + pixelFormat);
            var pixels = new byte[_width * _height * _pixelSize];
            image.CopyPixels(pixels, _width * _pixelSize, 0);
            _referenceGrayImage = ConvertToGrayscale(pixels);
            _roi = roi;
            _referenceFeatures = FindFirstFeatures();
        }

        private Point2f[] FindFirstFeatures()
        {
            const int PreferredMinStartFeatures = 50;
            const int MaxStartFeatures = 1000;
            const int MinFeatureDistance = 30;
            const int BlockSize = 7;

            var sw = Stopwatch.StartNew();
            using var mask = CreateRegionOfInterestMask()!;
            var qualityLevel = 0.01;
            var firstFeatures = _referenceGrayImage.GoodFeaturesToTrack(MaxStartFeatures, qualityLevel, MinFeatureDistance, mask, BlockSize, false, 0);
            if (firstFeatures.Length < PreferredMinStartFeatures)
            {
                qualityLevel *= 0.1;
                firstFeatures = _referenceGrayImage.GoodFeaturesToTrack(MaxStartFeatures, qualityLevel, MinFeatureDistance, mask, BlockSize, false, 0);
                if (firstFeatures.Length < PreferredMinStartFeatures)
                {
                    qualityLevel *= 0.1;
                    firstFeatures = _referenceGrayImage.GoodFeaturesToTrack(MaxStartFeatures, qualityLevel, MinFeatureDistance, mask, BlockSize, false, 0);
                }
            }
            Log.Write($"{firstFeatures.Length} features at quality level {qualityLevel} found in {sw.ElapsedMilliseconds} ms");
            if (firstFeatures.Length < MinimumFeatureCount)
                throw new UserMessageException("Not enough features found in reference image");
            return firstFeatures;
        }

        private Mat CreateMatFromPixels(byte[] pixels)
        {
            if (_pixelSize == 1)
                return Mat.FromPixelData(_height, _width, MatType.CV_8U, pixels);
            if (_pixelSize == 3)
                return Mat.FromPixelData(_height, _width, MatType.CV_8UC3, pixels);
            if (_pixelSize == 4)
                return Mat.FromPixelData(_height, _width, MatType.CV_8UC4, pixels);
            throw new UserMessageException("Unsupported number of planes: " + _pixelSize);
        }

        private Mat ConvertToGrayscale(byte[] pixels)
        {
            if (_pixelSize == 1)
                return Mat.FromPixelData(_height, _width, MatType.CV_8U, pixels);
            using var image = CreateMatFromPixels(pixels);
            return image.CvtColor(ColorConversionCodes.RGB2GRAY);
        }

        private Mat? CreateRegionOfInterestMask()
        {
            Mat? mask = null;
            if (_roi.HasValue)
            {
                mask = new Mat(_height, _width, MatType.CV_8U);
                mask.SetTo(new Scalar(0));
                mask.Rectangle(new Rect(_roi.Value.Left, _roi.Value.Top, _roi.Value.Width, _roi.Value.Height), new Scalar(255), -1);
            }
            return mask;
        }

        public void Apply(byte[] pixels)
        {
            var sw = Stopwatch.StartNew();
            using var image = CreateMatFromPixels(pixels);
            using var grayImage = _pixelSize == 1 ? image : image.CvtColor(ColorConversionCodes.BGR2GRAY); // Channel order is not important since it is only an internal image used for feature matching

            TrackFeatures(grayImage, out var features, out var status);
            var matchesCount = status.Count(s => s != 0);
            Log.Write($"{matchesCount}/{features.Length} features matched in {sw.ElapsedMilliseconds} ms");

            //SaveAnnotated(grayImage, features.Where((p, i) => status[i] != 0).Select(p => new Point((int)p.X, (int)p.Y)), @$"c:\temp\{++_frameCount}.jpg");

            if (matchesCount < MinimumMatches)
            {
                Log.Write("Not enough features matched");
                if (_reference == Reference.Previous)
                {
                    if (_previousTrans is not null)
                        WarpImage(image, pixels, _previousTrans);
                    _referenceGrayImage.Dispose();
                    _referenceGrayImage = grayImage.Clone();
                    _referenceFeatures = FindFirstFeatures();
                }
                return;
            }

            var trans = Cv2.FindHomography(features.Where((p, i) => status[i] != 0).Select(p => new Point2d(p.X, p.Y)),
                _referenceFeatures.Where((p, i) => status[i] != 0).Select(p => new Point2d(p.X, p.Y)), HomographyMethods.Ransac);

            if (_reference == Reference.Previous && _previousTrans is not null)
            {
                var toFirst = _previousTrans * trans;
                trans.Dispose();
                trans = toFirst;
            }

            WarpImage(image, pixels, trans);

            if (_reference == Reference.First)
                trans.Dispose();
            else if (_reference == Reference.Previous)
            {
                _referenceGrayImage.Dispose();
                _referenceGrayImage = grayImage.Clone();
                _previousTrans?.Dispose();
                _previousTrans = trans;
                if (matchesCount < MinimumFeatureCount)
                    _referenceFeatures = FindFirstFeatures();
                else
                    _referenceFeatures = features.Where((p, i) => status[i] != 0).ToArray();
            }
        }

        private void WarpImage(Mat source, byte[] target, Mat trans)
        {
            using var warped = source.WarpPerspective(trans, source.Size(), InterpolationFlags.Cubic,
                _borderHandling == Borders.Mirror ? BorderTypes.Reflect101 : BorderTypes.Constant, new Scalar(0));
            int size = _width * _height * _pixelSize;
            unsafe
            {
                fixed (byte* dst = target)
                    Buffer.MemoryCopy(warped.Ptr(0).ToPointer(), dst, size, size);
            }
            //warped.SaveImage(@$"c:\temp\{_frameCount}warped.jpg");
        }

        public Point2f GetTranslation(BitmapSource image)
        {
            if (_width != image.PixelWidth || _height != image.PixelHeight)
                throw new UserMessageException("Image size changed");
            var sw = Stopwatch.StartNew();
            var pixels = new byte[_width * _height * _pixelSize];
            image.CopyPixels(pixels, _width * _pixelSize, 0);
            using var grayImage = ConvertToGrayscale(pixels);

            TrackFeatures(grayImage, out var features, out var status);

            //SaveAnnotated(_firstGrayImage, _firstFeatures.Where((p, i) => status[i] != 0).Select(p => new Point((int)p.X, (int)p.Y)), @"c:\temp\1.jpg");
            //SaveAnnotated(grayImage, features.Where((p, i) => status[i] != 0).Select(p => new Point((int)p.X, (int)p.Y)), @"c:\temp\2.jpg");

            var xValues = new List<float>();
            var yValues = new List<float>();
            for (int i = 0; i < features.Length; i++)
                if (status[i] != 0)
                {
                    xValues.Add(_referenceFeatures[i].X - features[i].X);
                    yValues.Add(_referenceFeatures[i].Y - features[i].Y);
                }

            if (xValues.Count < features.Length * 0.8)
                throw new UserMessageException($"Not enough features matched ({xValues.Count}/{features.Length}={(double)xValues.Count / features.Length:F2})");

            xValues.Sort();
            yValues.Sort();
            var medianPoint = new Point2f(xValues[xValues.Count / 2], yValues[yValues.Count / 2]);

            Log.Write($"{xValues.Count}/{features.Length} ({(double)xValues.Count / features.Length:F2}) features matched in {sw.ElapsedMilliseconds} ms, median translation: ({IntMath.Round(medianPoint.X)},{IntMath.Round(medianPoint.Y)})");

            return medianPoint;
        }

        private void TrackFeatures(Mat grayImage, out Point2f[] features, out byte[] status)
        {
            features = new Point2f[_referenceFeatures.Length];
            Cv2.CalcOpticalFlowPyrLK(_referenceGrayImage, grayImage, _referenceFeatures, ref features, out status, out _,
                maxLevel: 10, minEigThreshold: 1e-4);
        }

#if DEBUG
        private static void SaveAnnotated(Mat image, IEnumerable<Point> points, string fileName)
        {
            using var annotated = image.Clone();
            int i = 0;
            foreach (var point in points)
            {
                Cv2.Circle(annotated, point, 5, new Scalar((i * 70) & 255), 1);
                i++;
            }
            annotated.SaveImage(fileName);
        }
#endif

        public void Dispose()
        {
            _referenceGrayImage.Dispose();
            _previousTrans?.Dispose();
        }
    }
}
