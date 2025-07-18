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
        PixelFormat _pixelFormat;
        RegistrationOperation? _registrationOperation;
        protected uint[]? _accumulatorPixels;

        protected int Width { get; private set; }
        protected int Height { get; private set; }
        protected int PixelSize { get; private set; }

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
            var result = BitmapSource.Create(Width, Height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, Width * PixelSize);
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
                Parallel.For(0, Width * Height, i => (resultPixels[i * 3], resultPixels[i * 3 + 2]) = (resultPixels[i * 3 + 2], resultPixels[i * 3]));
                pixelFormat16 = PixelFormats.Rgb48;
            }
            else if (_pixelFormat == PixelFormats.Gray8)
                pixelFormat16 = PixelFormats.Gray16;
            else
                throw new UserMessageException("Unsupported pixel format for 16 bit output" + _pixelFormat);
            var result = BitmapSource.Create(Width, Height, _dpiX, _dpiY, pixelFormat16, null, resultPixels, Width * PixelSize * 2);
            result.Freeze();
            return result;
        }

        protected abstract double GetResultScaling();

        protected byte[] PrepareFrame(BitmapSource image)
        {
            _ct.ThrowIfCancellationRequested();

            if (_accumulatorPixels is null)
            {
                Width = image.PixelWidth;
                Height = image.PixelHeight;
                _dpiX = image.DpiX;
                _dpiY = image.DpiY;
                _pixelFormat = image.Format;
                if (_pixelFormat == PixelFormats.Bgr32 || _pixelFormat == PixelFormats.Cmyk32)
                    PixelSize = 4;
                else if (_pixelFormat == PixelFormats.Rgb24 || _pixelFormat == PixelFormats.Bgr24)
                    PixelSize = 3;
                else if (_pixelFormat == PixelFormats.Gray8)
                    PixelSize = 1;
                else
                    throw new UserMessageException("Unsupported pixel format " + _pixelFormat);
                _accumulatorPixels = new uint[Width * Height * PixelSize];

                if (_darkFrame is not null)
                {
                    if (_darkFrame.PixelWidth != Width || _darkFrame.PixelHeight != Height)
                        throw new UserMessageException("Dark frame size does not match");
                    if (_darkFrame.Format != _pixelFormat)
                        throw new UserMessageException($"Dark frame pixel format {_darkFrame.Format} does not match frame format {_pixelFormat}");
                    _darkFramePixels = new byte[Width * Height * PixelSize];
                    _darkFrame.CopyPixels(_darkFramePixels, Width * PixelSize, 0);
                }
            }
            else if (_pixelFormat != image.Format)
                throw new UserMessageException("Pixel format changed");
            else if (Width != image.PixelWidth || Height != image.PixelHeight)
                throw new UserMessageException("Size changed");

            ProcessedImages++;

            var pixels = new byte[Width * Height * PixelSize];
            image.CopyPixels(pixels, Width * PixelSize, 0);

            SubtractDarkFrame(pixels);

            if (_registrationMethod > RegistrationMethod.None)
            {
                if (_registrationOperation is null)
                    _registrationOperation = new RegistrationOperation(pixels, Width, Height, PixelSize, _registrationMethod == RegistrationMethod.MirrorBorders, _registrationRegion);
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