using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    class CombineFramesOperation
    {
        readonly int _maxFrames;
        readonly CancellationToken _ct;

        PixelFormat _pixelFormat;
        int _width, _height, _planes, _pixelSize;
        double _dpiX, _dpiY;
        byte[]? _resultPixels;
        uint[]? _sumPixels;

        public CombineFramesOperation(int maxFrames, CancellationToken ct)
        {
            _maxFrames = maxFrames;
            _ct = ct;
        }

        public int ProcessedImages { get; private set; }

        public void UpdateMax(BitmapSource source)
        {
            if (ProcessedImages >= _maxFrames)
                return;

            var pixels = PrepareFrame(source);

            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i] > _resultPixels![i])
                    _resultPixels[i] = pixels[i];
        }

        internal void UpdateSum(BitmapSource source)
        {
            if (ProcessedImages >= _maxFrames)
                return;

            var pixels = PrepareFrame(source);
            if (_sumPixels is null)
                _sumPixels = new uint[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
                _sumPixels[i] += pixels[i];
        }

        public BitmapSource GetResult()
        {
            if (_resultPixels is null)
                throw new UserMessageException("No images received");
            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, _resultPixels, _width * _pixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource GetAverageResult()
        {
            if (_resultPixels is null || _sumPixels is null)
                throw new UserMessageException("No images received");
            for (int i = 0; i < _resultPixels.Length; i++)
                _resultPixels[i] = (byte)IntMath.Round(_sumPixels[i] / (double)ProcessedImages);
            return GetResult();
        }

        private byte[] PrepareFrame(BitmapSource source)
        {
            _ct.ThrowIfCancellationRequested();

            if (_resultPixels is null)
            {
                _width = source.PixelWidth;
                _height = source.PixelHeight;
                _dpiX = source.DpiX;
                _dpiY = source.DpiY;
                _pixelFormat = source.Format;
                if (source.Format == PixelFormats.Bgr32)
                {
                    _planes = 3; _pixelSize = 4;
                }
                else if (source.Format == PixelFormats.Rgb24 || source.Format == PixelFormats.Bgr24)
                {
                    _planes = 3; _pixelSize = 3;
                }
                else if (source.Format == PixelFormats.Cmyk32)
                {
                    _planes = 4; _pixelSize = 4;
                }
                else if (source.Format == PixelFormats.Gray8)
                {
                    _planes = 1; _pixelSize = 1;
                }
                else
                    throw new UserMessageException("Unsupported pixel format " + source.Format);

                _resultPixels = new byte[_width * _height * _pixelSize];
            }
            else if (_pixelFormat != source.Format)
                throw new UserMessageException("Pixel format changes");
            else if (_width != source.PixelWidth || _height != source.PixelHeight)
                throw new UserMessageException("Size changes");

            ProcessedImages++;

            var pixels = new byte[_width * _height * _pixelSize];
            source.CopyPixels(pixels, _width * _pixelSize, 0);
            return pixels;
        }
    }
}
