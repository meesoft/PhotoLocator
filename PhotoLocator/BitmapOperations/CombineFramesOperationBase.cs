using PhotoLocator.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    abstract class CombineFramesOperationBase : IDisposable
    {
        protected enum RegistrationMethod { None, BlackBorders, MirrorBorders };

        protected readonly CancellationToken _ct;
        readonly BitmapSource? _darkFrame;
        readonly RegistrationMethod _registrationMethod;
        readonly ROI? _registrationRegion;
        byte[]? _darkFramePixels;
        double _dpiX;
        double _dpiY;
        int _width;
        int _height;

        PixelFormat _pixelFormat;
        int _pixelSize;
        RegistrationOperation? _registrationOperation;
        protected uint[]? _accumulatorPixels;

        public int ProcessedImages { get; private set; }

        protected CombineFramesOperationBase(string? darkFramePath, RegistrationMethod registrationMethod, ROI? registrationRegion, CancellationToken ct)
        {
            _registrationMethod = registrationMethod;
            _registrationRegion = registrationRegion;
            _ct = ct;
            if (!string.IsNullOrEmpty(darkFramePath))
            {
                using var darkFrameStream = File.OpenRead(darkFramePath);
                var darkFrameDecoder = BitmapDecoder.Create(darkFrameStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                _darkFrame = darkFrameDecoder.Frames[0];
            }
        }

        public void Dispose()
        {
            _registrationOperation?.Dispose();
        }

        public abstract void ProcessImage(BitmapSource image);

        public bool Supports16BitResult()
        {
            return _pixelFormat == PixelFormats.Rgb24 || _pixelFormat == PixelFormats.Bgr24 || _pixelFormat == PixelFormats.Gray8;
        }

        public BitmapSource GetResult8()
        {
            if (_accumulatorPixels is null)
                throw new UserMessageException("No images received");
            var resultPixels = new byte[_accumulatorPixels.Length];
            var scaling = GetResultScaling();
            Parallel.For(0, _accumulatorPixels.Length, i => resultPixels[i] = (byte)(_accumulatorPixels[i] * scaling + 0.5));
            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * _pixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource GetResult16()
        {
            if (_accumulatorPixels is null)
                throw new UserMessageException("No images received");

            var resultPixels = new ushort[_accumulatorPixels.Length];
            var scaling = GetResultScaling() * 0xffff / 255.0;
            Parallel.For(0, resultPixels.Length, i => resultPixels[i] = (ushort)(_accumulatorPixels[i] * scaling + 0.5));
            PixelFormat pixelFormat16;
            if (_pixelFormat == PixelFormats.Rgb24)
                pixelFormat16 = PixelFormats.Rgb48;
            else if (_pixelFormat == PixelFormats.Bgr24)
            {
                Parallel.For(0, _width * _height, i => (resultPixels[i * 3], resultPixels[i * 3 + 2]) = (resultPixels[i * 3 + 2], resultPixels[i * 3]));
                pixelFormat16 = PixelFormats.Rgb48;
            }
            else if (_pixelFormat == PixelFormats.Gray8)
                pixelFormat16 = PixelFormats.Gray16;
            else
                throw new UserMessageException("Unsupported pixel format for 16 bit output" + _pixelFormat);
            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, pixelFormat16, null, resultPixels, _width * _pixelSize * 2);
            result.Freeze();
            return result;
        }

        protected abstract double GetResultScaling();

        protected byte[] PrepareFrame(BitmapSource image)
        {
            _ct.ThrowIfCancellationRequested();

            if (_accumulatorPixels is null)
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
                _accumulatorPixels = new uint[_width * _height * _pixelSize];

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

            SubtractDarkFrame(pixels);

            if (_registrationMethod > RegistrationMethod.None)
            {
                if (_registrationOperation is null)
                    _registrationOperation = new RegistrationOperation(pixels, _width, _height, _pixelSize, _registrationMethod == RegistrationMethod.MirrorBorders, _registrationRegion);
                else
                    _registrationOperation.Apply(pixels);
            }

            return pixels;
        }

        private void SubtractDarkFrame(byte[] pixels)
        {
            //TODO: This should be replaced by some proper hole closing where the dark frame has hot pixels
            if (_darkFramePixels is not null)
                Parallel.For(0, pixels.Length, i => pixels[i] = (byte)Math.Max(0, pixels[i] - _darkFramePixels[i]));
        }
    }
}