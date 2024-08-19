using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoLocator.Helpers;

namespace PhotoLocator.BitmapOperations
{
    class CombineFramesOperation
    {
        readonly CancellationToken _ct;
        readonly BitmapSource? _darkFrame;

        PixelFormat _pixelFormat;
        int _width, _height, _pixelSize;
        double _dpiX, _dpiY;
        byte[]? _resultPixels, _darkFramePixels;
        uint[]? _sumPixels;

        public CombineFramesOperation(string darkFramePath, CancellationToken ct = default)
        {
            _ct = ct;
            if (!string.IsNullOrEmpty(darkFramePath))
            {
                using var darkFrameStream = File.OpenRead(darkFramePath);
                var darkFrameDecoder = BitmapDecoder.Create(darkFrameStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                _darkFrame = darkFrameDecoder.Frames[0];
            }
        }

        public int ProcessedImages { get; private set; }

        public void UpdateMax(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            Parallel.For(0, pixels.Length, i =>
            {
                if (pixels[i] > _resultPixels![i])
                    _resultPixels[i] = pixels[i];
            });
        }

        internal void UpdateSum(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            _sumPixels ??= new uint[pixels.Length];

            Parallel.For(0, pixels.Length, i => _sumPixels[i] += pixels[i]);
        }

        public BitmapSource GetResult()
        {
            if (_resultPixels is null)
                throw new UserMessageException("No images received");
            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, _resultPixels, _width * _pixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource GetAverageResult8()
        {
            if (_resultPixels is null || _sumPixels is null)
                throw new UserMessageException("No images received");
            Parallel.For(0, _resultPixels.Length, i => _resultPixels[i] = (byte)IntMath.Round(_sumPixels[i] / (double)ProcessedImages));
            return GetResult();
        }

        public BitmapSource GetAverageResult16()
        {
            if (_resultPixels is null || _sumPixels is null)
                throw new UserMessageException("No images received");

            var resultPixels16 = new ushort[_resultPixels.Length];
            var scale = 0xffff / 255.0 / ProcessedImages;
            Parallel.For(0, resultPixels16.Length, i => resultPixels16[i] = (ushort)IntMath.Round(_sumPixels[i] * scale));
            PixelFormat pixelFormat16;
            if (_pixelFormat == PixelFormats.Rgb24)
                pixelFormat16 = PixelFormats.Rgb48;
            else if (_pixelFormat == PixelFormats.Bgr24)
            {
                Parallel.For(0, _width * _height, i => (resultPixels16[i * 3], resultPixels16[i * 3 + 2]) = (resultPixels16[i * 3 + 2], resultPixels16[i * 3]));
                pixelFormat16 = PixelFormats.Rgb48;
            }
            else if (_pixelFormat == PixelFormats.Gray8)
                pixelFormat16 = PixelFormats.Gray16;
            else
                throw new UserMessageException("Unsupported pixel format for 16 bit output" + _pixelFormat);
            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, pixelFormat16, null, resultPixels16, _width * _pixelSize * 2);
            result.Freeze();
            return result;
        }

        public bool Supports16BitAverage()
        {
            return _pixelFormat == PixelFormats.Rgb24 || _pixelFormat == PixelFormats.Bgr24 || _pixelFormat == PixelFormats.Gray8;
        }

        private byte[] PrepareFrame(BitmapSource image)
        {
            _ct.ThrowIfCancellationRequested();

            if (_resultPixels is null)
            {
                _width = image.PixelWidth;
                _height = image.PixelHeight;
                _dpiX = image.DpiX;
                _dpiY = image.DpiY;
                _pixelFormat = image.Format;
                if (_pixelFormat == PixelFormats.Bgr32 || _pixelFormat == PixelFormats.Cmyk32)
                    _pixelSize = 4;
                else if (_pixelFormat == PixelFormats.Rgb24 || _pixelFormat == PixelFormats.Bgr24)
                    _pixelSize = 3;
                else if (_pixelFormat == PixelFormats.Gray8)
                    _pixelSize = 1;
                else
                    throw new UserMessageException("Unsupported pixel format " + _pixelFormat);
                _resultPixels = new byte[_width * _height * _pixelSize];

                if (_darkFrame is not null)
                {
                    if (_darkFrame.PixelWidth != _width || _darkFrame.PixelHeight != _height)
                        throw new UserMessageException("Dark frame size does not match");
                    if (_darkFrame.Format != _pixelFormat)
                        throw new UserMessageException($"Dark frame pixel format {_darkFrame.Format} does not match frame format {_pixelFormat}");
                    _darkFramePixels = new byte[_width * _height * _pixelSize];
                    _darkFrame.CopyPixels(_darkFramePixels, _width * _pixelSize, 0);
                }
            }
            else if (_pixelFormat != image.Format)
                throw new UserMessageException("Pixel format changed");
            else if (_width != image.PixelWidth || _height != image.PixelHeight)
                throw new UserMessageException("Size changed");

            ProcessedImages++;

            var pixels = new byte[_width * _height * _pixelSize];
            image.CopyPixels(pixels, _width * _pixelSize, 0);
            if (_darkFramePixels is not null) //TODO: This should be replaced by some proper hole closing where the dark frame has hot pixels
                Parallel.For(0, pixels.Length, i => pixels[i] = (byte)Math.Max(0, pixels[i] - _darkFramePixels[i]));
            return pixels;
        }
    }
}
