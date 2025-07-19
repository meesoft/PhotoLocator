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
        readonly int _width;
        readonly int _height;
        readonly int _pixelSize;
        readonly bool _mirrorBorders;
        readonly Mat _firstGrayImage;
        readonly Point2f[] _firstFeatures;

        public RegistrationOperation(byte[] pixels, int width, int height, int pixelSize, bool mirrorBorders, ROI? roi = null)
        {
            _width = width;
            _height = height;
            _pixelSize = pixelSize;
            _mirrorBorders = mirrorBorders;
            _firstGrayImage = ConvertToGrayscale(pixels);
            _firstFeatures = FindFirstFeatures(roi);
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
            _firstGrayImage = ConvertToGrayscale(pixels);
            _firstFeatures = FindFirstFeatures(roi);
        }

        private Point2f[] FindFirstFeatures(ROI? roi)
        {
            var sw = Stopwatch.StartNew();
            using var mask = CreateRegionOfInterestMask(roi);
            var firstFeatures = _firstGrayImage.GoodFeaturesToTrack(1000, 0.01, 30, mask!, 7, false, 0);
            Log.Write($"{firstFeatures.Length} features found in {sw.ElapsedMilliseconds} ms");
            if (firstFeatures.Length < 10)
                throw new UserMessageException("Not enough features found in first image");
            return firstFeatures;
        }

        private Mat ConvertToGrayscale(byte[] pixels)
        {
            if (_pixelSize == 1)
                return Mat.FromPixelData(_height, _width, MatType.CV_8U, pixels);
            if (_pixelSize == 3)
            {
                using var image = Mat.FromPixelData(_height, _width, MatType.CV_8UC3, pixels);
                return image.CvtColor(ColorConversionCodes.RGB2GRAY);
            }
            if (_pixelSize == 4)
            {
                using var image = Mat.FromPixelData(_height, _width, MatType.CV_8UC4, pixels);
                return image.CvtColor(ColorConversionCodes.RGB2GRAY);
            }
            throw new UserMessageException("Unsupported number of planes: " + _pixelSize);
        }

        private Mat? CreateRegionOfInterestMask(ROI? roi)
        {
            Mat? mask = null;
            if (roi.HasValue)
            {
                mask = new Mat(_height, _width, MatType.CV_8U);
                mask.SetTo(new Scalar(0));
                mask.Rectangle(new Rect(roi.Value.Left, roi.Value.Top, roi.Value.Width, roi.Value.Height), new Scalar(255), -1);
            }
            return mask;
        }

        public void Apply(byte[] pixels)
        {
            using var image = Mat.FromPixelData(_height, _width, _pixelSize == 1 ? MatType.CV_8U : MatType.CV_8UC3, pixels);
            using var grayImage = _pixelSize == 1 ? image : image.CvtColor(ColorConversionCodes.RGB2GRAY);

            TrackFeatures(grayImage, out var features, out var status);

            using var trans = Cv2.FindHomography(
                features.Where((p, i) => status[i] != 0).Select(p => new Point2d(p.X, p.Y)),
                _firstFeatures.Where((p, i) => status[i] != 0).Select(p => new Point2d(p.X, p.Y)), HomographyMethods.Ransac);

            using var warped = image.WarpPerspective(trans, image.Size(), InterpolationFlags.Cubic, _mirrorBorders ? BorderTypes.Reflect101 : BorderTypes.Constant, new Scalar(0));

            int size = _width * _height * _pixelSize;
            unsafe
            {
                fixed (byte* dst = pixels)
                    Buffer.MemoryCopy(warped.Ptr(0).ToPointer(), dst, size, size);
            }
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
                    xValues.Add(_firstFeatures[i].X - features[i].X);
                    yValues.Add(_firstFeatures[i].Y - features[i].Y);
                }

            if (xValues.Count < features.Length * 0.8)
                throw new UserMessageException($"Not enough features matched ({xValues.Count}/{features.Length})");

            xValues.Sort();
            yValues.Sort();
            var medianPoint = new Point2f(xValues[xValues.Count / 2], yValues[yValues.Count / 2]);

            Log.Write($"{xValues.Count}/{features.Length} features matched in {sw.ElapsedMilliseconds} ms, median translation: ({IntMath.Round(medianPoint.X)},{IntMath.Round(medianPoint.Y)})");

            return medianPoint;
        }

        private void TrackFeatures(Mat grayImage, out Point2f[] features, out byte[] status)
        {
            features = new Point2f[_firstFeatures.Length];
            Cv2.CalcOpticalFlowPyrLK(_firstGrayImage, grayImage, _firstFeatures, ref features, out status, out _,
                maxLevel: 10, minEigThreshold: 0.001);
        }

        private static void SaveAnnotated(Mat image, IEnumerable<Point> points, string fileName)
        {
            int i = 0;
            foreach (var point in points)
            {
                Cv2.Circle(image, point, 5, new Scalar((i * 70) & 255), 1);
                i++;
            }
            image.SaveImage(fileName);
        }

        public void Dispose()
        {
            _firstGrayImage.Dispose();
        }
    }
}
