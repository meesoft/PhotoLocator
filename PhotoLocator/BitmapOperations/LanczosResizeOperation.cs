using PhotoLocator.Helpers;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    public class LanczosResizeOperation
    {
        //TODO: Consider proper gamma correction handling

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

        public Func<float, float> FilterFunc { get; set; } = Lanczos3;

        public float FilterWindow { get; set; } = 3;

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

            internal LineResampler(Func<float, float> filterFunc, float filterWindow, int srcWidth, int srcSampleDistance, int dstWidth)
            {
                // Compute filter weights for each position along the line
                _weights = new Weight[dstWidth];
                var scaleWN = (float)(srcWidth - 1) / Math.Max(dstWidth - 1, 1);
                var scaleNW = (float)(dstWidth - 1) / Math.Max(srcWidth - 1, 1);
                var reduceWindow = filterWindow * scaleWN;
                Parallel.For(0, dstWidth, i =>
                {
                    float sum = 0;
                    if (dstWidth < srcWidth) // Downscale
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - reduceWindow);
                        var pMax = (int)Math.Ceiling(center + reduceWindow);
                        var sourceWeights = new SourcePixelWeight[pMax - pMin + 1];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            SourcePixelWeight spw;
                            if (p < 0)
                                spw.SourceIndex = 0;
                            else if (p >= srcWidth)
                                spw.SourceIndex = (srcWidth - 1) * srcSampleDistance;
                            else
                                spw.SourceIndex = p * srcSampleDistance;
                            spw.SourceWeight = filterFunc((p - center) * scaleNW) * scaleNW;
                            sum += spw.SourceWeight;
                            sourceWeights[p - pMin] = spw;
                        }
                        _weights[i].SourcePixelWeights = sourceWeights;
                    }
                    else // Upscale
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - filterWindow);
                        var pMax = (int)Math.Ceiling(center + filterWindow);
                        var sourceWeights = new SourcePixelWeight[pMax - pMin + 1];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            SourcePixelWeight spw;
                            if (p < 0)
                                spw.SourceIndex = 0;
                            else if (p >= srcWidth)
                                spw.SourceIndex = (srcWidth - 1) * srcSampleDistance;
                            else
                                spw.SourceIndex = p * srcSampleDistance;
                            spw.SourceWeight = filterFunc(p - center);
                            sum += spw.SourceWeight;
                            sourceWeights[p - pMin] = spw;
                        }
                        _weights[i].SourcePixelWeights = sourceWeights;
                    }
                    if (sum > 0)
                    {
                        var scale = 1 / sum;
                        var sourceWeights = _weights[i].SourcePixelWeights;
                        for (var p = 0; p < sourceWeights.Length; p++)
                            sourceWeights[p].SourceWeight *= scale;
                    }
                });
            }

            public unsafe void Apply(byte* source, int srcOffset, byte* dest, int dstOffset, int dstSampleDistance)
            {
                var weights = _weights;
                for (var i = 0; i < weights.Length; i++)
                {
                    float sum = 0.5f;
                    int length = weights[i].SourcePixelWeights.Length;
                    fixed (SourcePixelWeight* sourceWeights = weights[i].SourcePixelWeights)
                    {
                        var sourceWeight = sourceWeights;
                        for (var j = 0; j < length; j++, sourceWeight++)
                            sum += source[srcOffset + (*sourceWeight).SourceIndex] * (*sourceWeight).SourceWeight;
                    }
                    dest[dstOffset + i * dstSampleDistance] = (byte)RealMath.Clamp(sum, 0, 255);
                }
            }
        }

        class LineResamplerFixedPoint
        {
            // Store indices and weights in separate arrays to reduce indirection and enable JIT optimization
            record struct Weight
            {
                public int[] SourceIndices;
                public int[] SourceWeights;
            }

            readonly Weight[] _weights;

            internal LineResamplerFixedPoint(Func<float, float> filterFunc, float filterWindow, int srcWidth, int srcSampleDistance, int dstWidth)
            {
                // Compute filter weights for each position along the line
                _weights = new Weight[dstWidth];
                var scaleWN = (float)(srcWidth - 1) / Math.Max(dstWidth - 1, 1);
                var scaleNW = (float)(dstWidth - 1) / Math.Max(srcWidth - 1, 1);
                var reduceWindow = filterWindow * scaleWN;
                Parallel.For(0, dstWidth, i =>
                {
                    double sum = 0;
                    float[] rawWeights;
                    int[] sourceIndices;
                    if (dstWidth < srcWidth) // Downscale
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - reduceWindow);
                        var pMax = (int)Math.Ceiling(center + reduceWindow);
                        var len = pMax - pMin + 1;
                        sourceIndices = new int[len];
                        rawWeights = new float[len];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            var idx = p - pMin;
                            if (p < 0)
                                sourceIndices[idx] = 0;
                            else if (p >= srcWidth)
                                sourceIndices[idx] = (srcWidth - 1) * srcSampleDistance;
                            else
                                sourceIndices[idx] = p * srcSampleDistance;
                            sum += rawWeights[idx] = filterFunc((p - center) * scaleNW) * scaleNW;
                        }
                    }
                    else // Upscale
                    {
                        var center = i * scaleWN;
                        var pMin = (int)Math.Floor(center - filterWindow);
                        var pMax = (int)Math.Ceiling(center + filterWindow);
                        var len = pMax - pMin + 1;
                        sourceIndices = new int[len];
                        rawWeights = new float[len];
                        for (var p = pMin; p <= pMax; p++)
                        {
                            var idx = p - pMin;
                            if (p < 0)
                                sourceIndices[idx] = 0;
                            else if (p >= srcWidth)
                                sourceIndices[idx] = (srcWidth - 1) * srcSampleDistance;
                            else
                                sourceIndices[idx] = p * srcSampleDistance;
                            sum += rawWeights[idx] = filterFunc(p - center);
                        }
                    }
                    if (sum > 0)
                    {
                        var scale = 65536 / sum;
                        var weightsInt = new int[rawWeights.Length];
                        for (var p = 0; p < rawWeights.Length; p++)
                            weightsInt[p] = IntMath.Round(rawWeights[p] * scale);
                        _weights[i].SourceIndices = sourceIndices;
                        _weights[i].SourceWeights = weightsInt;
                    }
                    else
                    {
                        _weights[i].SourceIndices = Array.Empty<int>();
                        _weights[i].SourceWeights = Array.Empty<int>();
                    }
                });
            }

            public unsafe void Apply(byte* source, int srcOffset, byte* dest, int dstOffset, int dstSampleDistance)
            {
                var weights = _weights;
                for (var i = 0; i < weights.Length; i++)
                {
                    int sum = 32768;
                    var sourceIndices = weights[i].SourceIndices;
                    var sourceWeights = weights[i].SourceWeights;
                    int length = sourceIndices.Length;
                    byte* sourcePixels = source + srcOffset;
                    int j = 0;
                    // Unroll loop to reduce per-iteration overhead and improve IL/JIT optimizations
                    for (; j + 3 < length; j += 4)
                        sum += sourcePixels[sourceIndices[j]] * sourceWeights[j]
                             + sourcePixels[sourceIndices[j + 1]] * sourceWeights[j + 1]
                             + sourcePixels[sourceIndices[j + 2]] * sourceWeights[j + 2]
                             + sourcePixels[sourceIndices[j + 3]] * sourceWeights[j + 3];
                    for (; j < length; j++)
                        sum += sourcePixels[sourceIndices[j]] * sourceWeights[j];
                    if (sum > 0)
                    {
                        sum >>= 16;
                        if (sum > 255)
                            sum = 255;
                        dest[dstOffset + i * dstSampleDistance] = (byte)sum;
                    }
                }
            }
        }

        public byte[] Apply(byte[] pixels, int width, int height, int planes, int pixelSize, int newWidth, int newHeight, CancellationToken ct)
        {
            if (width != newWidth)
            {
                var resampler = new LineResamplerFixedPoint(FilterFunc, FilterWindow, width, pixelSize, newWidth);
                var dstPixels = new byte[newWidth * height * pixelSize];
                Parallel.For(0, height, y =>
                {
                    unsafe
                    {
                        fixed (byte* src = &pixels[y * width * pixelSize])
                        fixed (byte* dst = &dstPixels[y * newWidth * pixelSize])
                            for (var p = 0; p < planes; p++)
                                resampler.Apply(src, p, dst, p, pixelSize);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                pixels = dstPixels;
                width = newWidth;
            }
            if (height != newHeight)
            {
                var resampler = new LineResamplerFixedPoint(FilterFunc, FilterWindow, height, width * pixelSize, newHeight);
                var dstPixels = new byte[newWidth * newHeight * pixelSize];
                Parallel.For(0, width, x =>
                {
                    unsafe
                    {
                        fixed (byte* src = &pixels[x * pixelSize])
                        fixed (byte* dst = &dstPixels[x * pixelSize])
                            for (var p = 0; p < planes; p++)
                                resampler.Apply(src, p, dst, p, pixelSize * width);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                pixels = dstPixels;
                height = newHeight;
            }
            return pixels;
        }

        public BitmapSource? Apply(BitmapSource source, int newWidth, int newHeight,
            double newDpiX, double newDpiY, CancellationToken ct)
        {
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            if (width == newWidth && height == newHeight && source.DpiX == newDpiX && source.DpiY == newDpiY)
                return source;

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
                return null;

            var srcPixels = ArrayPool<byte>.Shared.Rent(width * height * pixelSize);
            source.CopyPixels(srcPixels, width * pixelSize, 0);

            var dstPixels = Apply(srcPixels, width, height, planes, pixelSize, newWidth, newHeight, ct);
            var result = BitmapSource.Create(newWidth, newHeight, newDpiX, newDpiY, source.Format, null, dstPixels, newWidth * pixelSize);
            ArrayPool<byte>.Shared.Return(srcPixels); // Apply may return srcPixels so only return after creating result

            result.Freeze();
            return result;
        }
    }
}