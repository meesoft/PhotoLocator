using OpenCvSharp;
using PhotoLocator.Helpers;
using System;
using System.Linq;

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

        public RegistrationOperation(byte[] pixels, int width, int height, int pixelSize, bool mirrorBorders, ROI? roi)
        {
            _width = width;
            _height = height;
            _pixelSize = pixelSize;
            _mirrorBorders = mirrorBorders;
            
            _firstGrayImage = ConvertTyGrayscale(pixels);

            Mat mask = null!;
            if (roi.HasValue)
            {
                mask = new Mat(height, width, MatType.CV_8U);
                mask.SetTo(new Scalar(0));
                mask.Rectangle(new Rect(roi.Value.Left, roi.Value.Top, roi.Value.Width, roi.Value.Height), new Scalar(255), -1);
            }
            _firstFeatures = _firstGrayImage.GoodFeaturesToTrack(1000, 0.005, 50, mask, 7, false, 0);
            mask?.Dispose();
            if (_firstFeatures.Length < 10)
                throw new UserMessageException("Not enough features found in first image");
        }

        private Mat ConvertTyGrayscale(byte[] pixels)
        {
            if (_pixelSize == 1)
                return Mat.FromPixelData(_height, _width, MatType.CV_8U, pixels);
            if (_pixelSize == 3)
            {
                using var image = Mat.FromPixelData(_height, _width, MatType.CV_8UC3, pixels);
                return image.CvtColor(ColorConversionCodes.RGB2GRAY);
            }
            throw new UserMessageException("Unsupported number of planes: " + _pixelSize);
        }

        public void Apply(byte[] pixels)
        {
            using var image = Mat.FromPixelData(_height, _width, MatType.CV_8UC3, pixels);
            using var grayImage = _pixelSize == 1 ? image : image.CvtColor(ColorConversionCodes.RGB2GRAY);

            var features = new Point2f[_firstFeatures.Length];
            Cv2.CalcOpticalFlowPyrLK(_firstGrayImage, grayImage, _firstFeatures, ref features, out var status, out var errors, maxLevel: 10);

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

        public void Dispose()
        {
            _firstGrayImage.Dispose();
        }
    }
}
