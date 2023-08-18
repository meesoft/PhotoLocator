using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    public class LanczosResizeOperation
    {
        /// <see cref="http://en.wikipedia.org/wiki/Lanczos_resampling"/>
        public static float Lanczos2(float x)
        {
            const double a = 2;
            x = Math.Abs(x);
            if (x < 1e-8f) return 1;
            else if (x >= a) return 0;
            else return (float)(a * Math.Sin(Math.PI * x) * Math.Sin(Math.PI / a * x) / RealMath.Sqr(Math.PI * x));
        }

        public static float Lanczos3(float x)
        {
            const double a = 3;
            x = Math.Abs(x);
            if (x < 1e-8f) return 1;
            else if (x >= a) return 0;
            else return (float)(a * Math.Sin(Math.PI * x) * Math.Sin(Math.PI / a * x) / RealMath.Sqr(Math.PI * x));
        }

        public Func<float, float> FilterFunc = Lanczos3;

        public float FilterWindow = 3;

        class LineResampler
        {
            record struct SourcePixelWeight
            {
                public int SourceIndex;
                public float SourceWeight;
            }

            record struct Weight
            {
                public SourcePixelWeight[] SourcePixelWeights;
            }

            readonly Weight[] _weights;

            public LineResampler(Func<float, float> filterFunc, float filterWindow, int srcWidth, int srcSampleDist, int dstWidth)
            {
                // Compute filter weights for each position along the line
                _weights = new Weight[dstWidth];
                var scaleWN = (float)(srcWidth - 1) / Math.Max(dstWidth - 1, 1);
                var scaleNW = (float)(dstWidth - 1) / Math.Max(srcWidth - 1, 1);
                var reduceWindow = filterWindow * scaleWN;
                Parallel.For(0, dstWidth, i =>
                {
                    float sum = 0;
                    if (dstWidth < srcWidth)
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - reduceWindow);
                        var pMax = (int)Math.Ceiling(center + reduceWindow);
                        _weights[i].SourcePixelWeights = new SourcePixelWeight[pMax - pMin + 1];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            SourcePixelWeight spw;
                            if (p < 0)
                                spw.SourceIndex = 0;
                            else if (p >= srcWidth)
                                spw.SourceIndex = (srcWidth - 1) * srcSampleDist;
                            else
                                spw.SourceIndex = p * srcSampleDist;
                            spw.SourceWeight = filterFunc((p - center) * scaleNW) * scaleNW;
                            sum += spw.SourceWeight;
                            _weights[i].SourcePixelWeights[p - pMin] = spw;
                        }
                    }
                    else
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - filterWindow);
                        var pMax = (int)Math.Ceiling(center + filterWindow);
                        _weights[i].SourcePixelWeights = new SourcePixelWeight[pMax - pMin + 1];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            SourcePixelWeight spw;
                            if (p < 0)
                                spw.SourceIndex = 0;
                            else if (p >= srcWidth)
                                spw.SourceIndex = (srcWidth - 1) * srcSampleDist;
                            else
                                spw.SourceIndex = p * srcSampleDist;
                            spw.SourceWeight = filterFunc(p - center);
                            sum += spw.SourceWeight;
                            _weights[i].SourcePixelWeights[p - pMin] = spw;
                        }
                    }
                    if (sum > 0)
                    {
                        var scale = 1 / sum;
                        for (var p = 0; p < _weights[i].SourcePixelWeights.Length; p++)
                            _weights[i].SourcePixelWeights[p].SourceWeight *= scale;
                    }
                });
            }

            public unsafe void Apply(byte* source, int srcOffset, byte* dest, int dstOffset, int dstSampleDist)
            {
                for (var i = 0; i < _weights.Length; i++)
                {
                    float sum = 0;
                    for (var j = 0; j < _weights[i].SourcePixelWeights.Length; j++)
                    {
                        var sample = source[srcOffset + _weights[i].SourcePixelWeights[j].SourceIndex];
                        sum += sample * _weights[i].SourcePixelWeights[j].SourceWeight;
                    }
                    dest[dstOffset + i * dstSampleDist] = (byte)RealMath.EnsureRange(sum + 0.5f, 0, 255);
                }
            }
        }

        private BitmapSource Apply8(BitmapSource source, int planes, int pixelSize, int newWidth, int newHeight,
            double newDpiX, double newDpiY, CancellationToken ct)
        {
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            if (width == newWidth && height == newHeight)
                return source;

            var pixels = new byte[width * height * pixelSize];
            source.CopyPixels(pixels, width * pixelSize, 0);

            if (width != newWidth)
            {
                var horzResampler = new LineResampler(FilterFunc, FilterWindow, width, pixelSize, newWidth);
                var dstPixels = new byte[newWidth * height * pixelSize];
                Parallel.For(0, height, y =>
                {
                    unsafe
                    {
                        fixed (byte* src = &pixels[y * width * pixelSize])
                        fixed (byte* dst = &dstPixels[y * newWidth * pixelSize])
                            for (var p = 0; p < planes; p++)
                                horzResampler.Apply(src, p, dst, p, pixelSize);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                pixels = dstPixels;
                width = newWidth;
            }
            if (height != newHeight)
            {
                var horzResampler = new LineResampler(FilterFunc, FilterWindow, height, width * pixelSize, newHeight);
                var dstPixels = new byte[newWidth * newHeight * pixelSize];
                Parallel.For(0, width, x =>
                {
                    unsafe
                    {
                        fixed (byte* src = &pixels[x * pixelSize])
                        fixed (byte* dst = &dstPixels[x * pixelSize])
                            for (var p = 0; p < planes; p++)
                                horzResampler.Apply(src, p, dst, p, pixelSize * width);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                pixels = dstPixels;
                height = newHeight;
            }
            var result = BitmapSource.Create(width, height, newDpiX, newDpiY, source.Format, null, pixels, width * pixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource? Apply(BitmapSource source, int newWidth, int newHeight,
            double newDpiX, double newDpiY, CancellationToken ct)
        {
            int planes, pixelSize;
            if (source.Format == PixelFormats.Bgr32)
            {
                planes = 3; pixelSize = 4;
            }
            else if (source.Format == PixelFormats.Rgb24 || source.Format == PixelFormats.Bgr24)
            {
                planes = 3; pixelSize = 3;
            }
            else if (source.Format == PixelFormats.Cmyk32)
            {
                planes = 4; pixelSize = 4;
            }
            else if (source.Format == PixelFormats.Gray8)
            {
                planes = 1; pixelSize = 1;
            }
            else
            {
                return null;
            }
            return Apply8(source, planes, pixelSize, newWidth, newHeight, newDpiX, newDpiY, ct);
        }
    }
}