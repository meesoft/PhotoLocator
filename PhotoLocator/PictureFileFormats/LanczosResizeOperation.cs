using PhotoLocator.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MeeSoft.ImageProcessing.Operations
{
    public class LanczosResizeOperation
    {
        /// <see cref="http://en.wikipedia.org/wiki/Sinc_filter"/>
        public static float Sinc(float x)
        {
            if (Math.Abs(x) < 1e-8f)
                return 1;
            else
            {
                var xPi = x * Math.PI;
                return (float)(Math.Sin(xPi) / xPi);
            }
        }

        /// <see cref="http://en.wikipedia.org/wiki/Lanczos_resampling"/>
        public static float Lanczos2(float x)
        {
            const double a = 2;
            x = Math.Abs(x);
            if (x < 1e-8f) return 1;
            else if (x >= a) return 0;
            else return (float)(a * Math.Sin(Math.PI * x) * Math.Sin((Math.PI / a) * x) / RealMath.Sqr(Math.PI * x));
        }

        /// <see cref="http://en.wikipedia.org/wiki/Lanczos_resampling"/>
        public static float Lanczos3(float x)
        {
            const double a = 3;
            x = Math.Abs(x);
            if (x < 1e-8f) return 1;
            else if (x >= a) return 0;
            else return (float)(a * Math.Sin(Math.PI * x) * Math.Sin((Math.PI / a) * x) / RealMath.Sqr(Math.PI * x));
        }

        public Func<float, float> FilterFunc = Lanczos2;

        public float FilterWindow = 2;

        protected class LineResampler
        {
            record struct SourcePixelWeight
            {
                public int SourceIndex;
                public float SourceWeight;
            }

            record struct Weight
            {
                public SourcePixelWeight[] SourcePixelWeights;
                public float Scale;
            }

            readonly Weight[] _weights;

            public LineResampler(Func<float, float> filterFunc, float filterWindow, int srcWidth, int srcSampleDist, int dstWidth)
            {
                // Compute filter weights for each position along the line
                _weights = new Weight[dstWidth];
                float ScaleWN = (float)(srcWidth - 1) / Math.Max(dstWidth - 1, 1);
                float ScaleNW = (float)(dstWidth - 1) / Math.Max(srcWidth - 1, 1);
                if (dstWidth < srcWidth)
                {
                    float Window = filterWindow * ScaleWN;
                    for (int i = 0; i < dstWidth; i++)
                    {
                        float center = i * ScaleWN;
                        int pMin = (int)Math.Floor(center - Window);
                        int pMax = (int)Math.Ceiling(center + Window);
                        _weights[i].SourcePixelWeights = new SourcePixelWeight[pMax - pMin + 1];
                        float sum = 0;
                        for (int p = pMin; p <= pMax; p++)
                        {
                            SourcePixelWeight spw;
                            if (p < 0)
                                spw.SourceIndex = 0;
                            else if (p >= srcWidth)
                                spw.SourceIndex = (srcWidth - 1) * srcSampleDist;
                            else
                                spw.SourceIndex = p * srcSampleDist;
                            spw.SourceWeight = filterFunc((p - center) * ScaleNW) * ScaleNW;
                            sum += spw.SourceWeight;
                            _weights[i].SourcePixelWeights[p - pMin] = spw;
                        }
                        if (sum > 0)
                            _weights[i].Scale = 1 / sum;
                    }
                }
                else
                {
                    for (int i = 0; i < dstWidth; i++)
                    {
                        float center = i * ScaleWN;
                        int pMin = (int)Math.Floor(center - filterWindow);
                        int pMax = (int)Math.Ceiling(center + filterWindow);
                        _weights[i].SourcePixelWeights = new SourcePixelWeight[pMax - pMin + 1];
                        float sum = 0;
                        for (int p = pMin; p <= pMax; p++)
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
                        if (sum > 0)
                            _weights[i].Scale = 1 / sum;
                    }
                }
                // Normalize filter weights
                for (int i = 0; i < dstWidth; i++)
                    for (int p = 0; p < _weights[i].SourcePixelWeights.Length; p++)
                        _weights[i].SourcePixelWeights[p].SourceWeight *= _weights[i].Scale;
            }

            public unsafe void Apply(byte* source, int srcOffset, byte* dest, int dstOffset, int dstSampleDist)
            {
                for (int i = 0; i < _weights.Length; i++)
                {
                    float sum = 0;
                    for (int j = 0; j < _weights[i].SourcePixelWeights.Length; j++)
                    {
                        var sample = source[srcOffset + _weights[i].SourcePixelWeights[j].SourceIndex];
                        sum += sample * _weights[i].SourcePixelWeights[j].SourceWeight;
                    }
                    dest[dstOffset + i * dstSampleDist] = (byte)RealMath.EnsureRange(sum + 0.5f, 0, 255);
                }
            }
        }

        public BitmapSource Apply8(BitmapSource source, int planes, int pixelSize, int newWidth, int newHeight, 
            double newDpiX, double newDpiY, CancellationToken ct)
        {
            var srcWidth = source.PixelWidth;
            var srcHeight = source.PixelHeight;
            if (srcWidth == newWidth && srcHeight == newHeight)
                return source;

            var srcPixels = new byte[srcWidth * srcHeight * pixelSize];
            source.CopyPixels(srcPixels, srcWidth * pixelSize, 0);

            if (srcWidth != newWidth)
            { 
                var horzResampler = new LineResampler(FilterFunc, FilterWindow, srcWidth, pixelSize, newWidth);
                var dstPixels = new byte[newWidth * srcHeight * pixelSize];
                Parallel.For(0, srcHeight, y =>
                {
                    unsafe
                    {
                        fixed (byte* src = &srcPixels[y * srcWidth * pixelSize])
                        fixed (byte* dst = &dstPixels[y * newWidth * pixelSize])
                            for (int p = 0; p < planes; p++)
                                horzResampler.Apply(src, p, dst, p, pixelSize);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                srcPixels = dstPixels; 
                srcWidth = newWidth;
            }
            if (srcHeight != newHeight)
            {
                var horzResampler = new LineResampler(FilterFunc, FilterWindow, srcHeight, srcWidth * pixelSize, newHeight);
                var dstPixels = new byte[newWidth * newHeight * pixelSize];
                Parallel.For(0, srcWidth, x =>
                {
                    unsafe
                    {
                        fixed (byte* src = &srcPixels[x * pixelSize])
                        fixed (byte* dst = &dstPixels[x * pixelSize])
                            for (int p = 0; p < planes; p++)
                                horzResampler.Apply(src, p, dst, p, pixelSize * srcWidth);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                srcPixels = dstPixels;
                srcHeight = newHeight;
            }
            var result = BitmapSource.Create(newWidth, newHeight, newDpiX, newDpiY, source.Format, null, srcPixels, newWidth * pixelSize);
            result.Freeze();
            return result;
        }
    }
}
