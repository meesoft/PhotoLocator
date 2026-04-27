using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    public class FloatBitmap
    {
        public const double DefaultMonitorGamma = 2.2;

        public FloatBitmap()
        {
        }

        public FloatBitmap(int width, int height, int planes)
        {
            New(width, height, planes);
        }

        public FloatBitmap(FloatBitmap source)
        {
            Assign(source);
        }

        public FloatBitmap(BitmapSource source, double gamma)
        {
            Assign(source, gamma);
        }

        public float[,] Elements { get; private set; } = null!;

        public float this[int x, int y]
        {
            get { return Elements[y, x]; }
            set { Elements[y, x] = value; }
        }

        /// <summary> Gray, RGB or CMYK </summary>
        public int PlaneCount { get; private set; }

        public int Stride => Elements.GetLength(1);

        public int Width { get; private set; }

        public int Height => Elements.GetLength(0);

        public int Size => Stride * Height;

        public override string ToString()
        {
            return $"{Width}x{Height}x{PlaneCount}";
        }

        public void New(int width, int height, int planes)
        {
            var stride = width * planes;
            if (Elements == null || stride != Stride || height != Height)
                Elements = new float[height, stride];
            Width = width;
            PlaneCount = planes;
        }

        public void Assign(FloatBitmap source)
        {
            New(source.Width, source.Height, source.PlaneCount);
            Array.Copy(source.Elements, Elements, Stride * Height);
        }

        public void Assign(BitmapSource source, double gamma)
        {
            if (source.Format == PixelFormats.Rgb48 || source.Format == PixelFormats.Gray16)
            {
                New(source.PixelWidth, source.PixelHeight, source.Format == PixelFormats.Rgb48 ? 3 : 1);
                var sourcePixels = new ushort[Height * Stride];
                source.CopyPixels(sourcePixels, Stride * 2, 0);
                var gammaLut = CreateDeGammaLookup(gamma, 65536);
                unsafe
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* gamma = gammaLut)
                        fixed (float* dstRow = &Elements[y, 0])
                        fixed (ushort* srcRow = &sourcePixels[y * Stride])
                        {
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                                dstRow[x] = gamma[srcRow[x]];
                        }
                    });
                }
                ArrayPool<float>.Shared.Return(gammaLut);
            }
            else if (source.Format == PixelFormats.Bgr24)
            {
                New(source.PixelWidth, source.PixelHeight, 3);
                var sourcePixels = ArrayPool<byte>.Shared.Rent(Height * Stride);
                source.CopyPixels(sourcePixels, Stride, 0);
                var gammaLut = CreateDeGammaLookup(gamma, 256);
                unsafe
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* gamma = gammaLut)
                        fixed (float* dstRow = &Elements[y, 0])
                        fixed (byte* srcRow = &sourcePixels[y * Stride])
                        {
                            var width = Width;
                            for (var x = 0; x < width; x++)
                            {
                                dstRow[x * 3 + 2] = gamma[srcRow[x * 3 + 0]];
                                dstRow[x * 3 + 1] = gamma[srcRow[x * 3 + 1]];
                                dstRow[x * 3 + 0] = gamma[srcRow[x * 3 + 2]];
                            }
                        }
                    });
                }
                ArrayPool<float>.Shared.Return(gammaLut);
                ArrayPool<byte>.Shared.Return(sourcePixels);

            }
            else if (source.Format == PixelFormats.Bgr32)
            {
                New(source.PixelWidth, source.PixelHeight, 3);
                var sourcePixels = ArrayPool<byte>.Shared.Rent(Height * Width * 4);
                source.CopyPixels(sourcePixels, Width * 4, 0);
                var gammaLut = CreateDeGammaLookup(gamma, 256);
                unsafe
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* gamma = gammaLut)
                        fixed (float* dstRow = &Elements[y, 0])
                        fixed (byte* srcRow = &sourcePixels[y * Width * 4])
                        {
                            var width = Width;
                            for (var x = 0; x < width; x++)
                            {
                                dstRow[x * 3 + 2] = gamma[srcRow[x * 4 + 0]];
                                dstRow[x * 3 + 1] = gamma[srcRow[x * 4 + 1]];
                                dstRow[x * 3 + 0] = gamma[srcRow[x * 4 + 2]];
                            }
                        }
                    });
                }
                ArrayPool<float>.Shared.Return(gammaLut);
                ArrayPool<byte>.Shared.Return(sourcePixels);
            }
            else
            {
                int planes;
                if (source.Format == PixelFormats.Gray8)
                    planes = 1;
                else if (source.Format == PixelFormats.Rgb24)
                    planes = 3;
                else if (source.Format == PixelFormats.Cmyk32)
                    planes = 4;
                else
                    throw new UserMessageException("Unsupported pixel format " + source.Format);
                New(source.PixelWidth, source.PixelHeight, planes);
                var sourcePixels = ArrayPool<byte>.Shared.Rent(Height * Stride);
                source.CopyPixels(sourcePixels, Stride, 0);
                var gammaLut = CreateDeGammaLookup(gamma, 256);
                unsafe
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* gamma = gammaLut)
                        fixed (float* dstRow = &Elements[y, 0])
                        fixed (byte* srcRow = &sourcePixels[y * Stride])
                        {
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                                dstRow[x] = gamma[srcRow[x]];
                        }
                    });
                }
                ArrayPool<float>.Shared.Return(gammaLut);
                ArrayPool<byte>.Shared.Return(sourcePixels);
            }
        }

        public void Assign(BitmapPlaneInt16 src, Func<short, float> remap)
        {
            New(src.Width, src.Height, 1);
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (Int16* srcPix = &src.Elements[y, 0])
                    fixed (float* dstPix = &Elements[y, 0])
                    {
                        var width = Width;
                        for (int x = 0; x < width; x++)
                            dstPix[x] = remap(srcPix[x]);
                    }
                });
            }
        }

        public BitmapSource ToBitmapSource(double dpiX, double dpiY, double gamma, PixelFormat? format = null)
        {
            var pixels = ToPixels8(gamma);
            var bitmap = CreateBitmapSource(dpiX, dpiY, format, pixels);
            ArrayPool<byte>.Shared.Return(pixels);
            return bitmap;
        }

        public (BitmapSource Bitmap, Task<int[]> Histogram) ToBitmapSourceWithHistogram(double dpiX, double dpiY, double gamma, PixelFormat? format = null)
        {
            var pixels = ToPixels8(gamma);
            var bitmap = CreateBitmapSource(dpiX, dpiY, format, pixels);
            return (bitmap, Task.Run(() =>
            {
                var histogram = new int[256];
                var size = Size;
                for (int i = 0; i < size; i++)
                    histogram[pixels[i]]++;
                ArrayPool<byte>.Shared.Return(pixels);
                return histogram;
            }));
        }

        private byte[] ToPixels8(double gamma)
        {
            if (Elements is null)
                throw new InvalidOperationException("Bitmap not initialized");
            var pixels = ArrayPool<byte>.Shared.Rent(Height * Stride);
            var gammaLut = CreateGammaLookupFloatToByte(gamma);
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    var stride = Stride;
                    fixed (byte* gamma = gammaLut)
                    fixed (float* srcRow = &Elements[y, 0])
                    fixed (byte* dstRow = &pixels[y * stride])
                    {
                        for (var x = 0; x < stride; x++)
                        {
                            int idx = (int)(srcRow[x] * FloatToByteGammaLutRange + 0.5f);
                            if (idx < 0) idx = 0; else if (idx > FloatToByteGammaLutRange) idx = FloatToByteGammaLutRange;
                            dstRow[x] = gamma[idx];
                        }
                    }
                });
            }
            ArrayPool<byte>.Shared.Return(gammaLut);
            return pixels;
        }

        private BitmapSource CreateBitmapSource(double dpiX, double dpiY, PixelFormat? format, byte[] pixels)
        {
            if (!format.HasValue)
                format = PlaneCount switch
                {
                    1 => PixelFormats.Gray8,
                    3 => PixelFormats.Rgb24,
                    4 => PixelFormats.Cmyk32,
                    _ => throw new UserMessageException("Unsupported number of planes: " + PlaneCount)
                };
            var result = BitmapSource.Create(Width, Height, dpiX, dpiY, format.Value, null, pixels, Stride);
            result.Freeze();
            return result;
        }

        public void SaveToFile(string fileName, double gamma = 1)
        {
            GeneralFileFormatHandler.SaveToFile(ToBitmapSource(96, 96, gamma), fileName);
        }

        internal static float[] CreateDeGammaLookup(double gamma, int range)
        {
            var gammaLUT = ArrayPool<float>.Shared.Rent(range);
            var scale = 1.0 / (range - 1);
            for (int i = 0; i < range; i++)
                gammaLUT[i] = (float)Math.Pow(i * scale, gamma);
            return gammaLUT;
        }

        internal const int FloatToByteGammaLutRange = 100_000;

        internal static byte[] CreateGammaLookupFloatToByte(double gamma)
        {
            var gammaLUT = ArrayPool<byte>.Shared.Rent(FloatToByteGammaLutRange + 1);
            gamma = 1 / gamma;
            Parallel.For(0, FloatToByteGammaLutRange + 1,
                i => gammaLUT[i] = (byte)(Math.Pow(i / (double)FloatToByteGammaLutRange, gamma) * 255 + 0.5));
            return gammaLUT;
        }

        /// <summary>
        /// Clamp x,y inside image bounds
        /// </summary>
        public float GetPixelSafe(int x, int y)
        {
            if (x < 0)
                x = 0;
            if (x >= Stride)
                x = Stride - 1;
            if (y < 0)
                y = 0;
            if (y >= Height)
                y = Height - 1;
            return Elements[y, x];
        }

        /// <summary>
        /// Interpolate and clamp x,y inside image bounds
        /// </summary>
        public float GetPixelInterpolate(float x, float y)
        {
            var width = Stride;
            var height = Height;

            if (x < 0)
                x = 0;
            else if (x > width)
                x = width;
            if (y < 0)
                y = 0;
            else if (y > height)
                y = height;

            var ix = (int)x;
            var iy = (int)y;

            if (ix < width - 1)
                x -= ix;
            else
            {
                ix = width - 2;
                x = 1f;
            }
            if (iy < height - 1)
                y -= iy;
            else
            {
                iy = height - 2;
                y = 1f;
            }

            unsafe
            {
                fixed (float* elements = Elements)
                {
                    var element = &elements[iy * width + ix];
                    return (element[0] * (1 - x) + element[1] * x) * (1 - y) +
                           (element[width] * (1 - x) + element[width + 1] * x) * y;
                }
            }/**/

            /*return (_elements[iy, ix] * (1 - x) + _elements[iy, ix + 1] * (x)) * (1 - y) +
                   (_elements[iy + 1, ix] * (1 - x) + _elements[iy + 1, ix + 1] * (x)) * (y);/**/
        }

        public float Min()
        {
            float min = float.PositiveInfinity;
            unsafe
            {
                fixed (float* elements = Elements)
                {
                    var size = Size;
                    for (var i = 0; i < size; i++)
                        min = Math.Min(min, elements[i]);
                }
            }
            return min;
        }

        public (float Min, float Max) MinMax()
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            unsafe
            {
                fixed (float* elements = Elements)
                {
                    var p = elements;
                    // Process in blocks of 8 to reduce loop overhead and branch misprediction
                    var remaining = Size;
                    while (remaining >= 8)
                    {
                        var v0 = p[0]; if (v0 < min) min = v0; if (v0 > max) max = v0;
                        var v1 = p[1]; if (v1 < min) min = v1; if (v1 > max) max = v1;
                        var v2 = p[2]; if (v2 < min) min = v2; if (v2 > max) max = v2;
                        var v3 = p[3]; if (v3 < min) min = v3; if (v3 > max) max = v3;
                        var v4 = p[4]; if (v4 < min) min = v4; if (v4 > max) max = v4;
                        var v5 = p[5]; if (v5 < min) min = v5; if (v5 > max) max = v5;
                        var v6 = p[6]; if (v6 < min) min = v6; if (v6 > max) max = v6;
                        var v7 = p[7]; if (v7 < min) min = v7; if (v7 > max) max = v7;
                        p += 8;
                        remaining -= 8;
                    }
                    // Finish remaining elements
                    for (var i = 0; i < remaining; i++)
                    {
                        var value = *p++;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }
            return (min, max);
        }

        public double Mean()
        {
            double sum = 0;
            var size = Size;
            unsafe
            {
                fixed (float* elements = Elements)
                {
                    float* p = elements;
                    var remaining = size;
                    while (remaining >= 8)
                    {
                        sum += p[0] + p[1] + p[2] + p[3] + p[4] + p[5] + p[6] + p[7];
                        p += 8;
                        remaining -= 8;
                    }
                    for (var i = 0; i < remaining; i++)
                        sum += *p++;
                }
            }
            return sum / size;
        }

        /// <summary>
        /// Apply operation to all elements
        /// </summary>
        public void ProcessElementWise(Func<int, int, float> operation)
        {
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* elements = &Elements[y, 0])
                    {
                        var stride = Stride;
                        for (var x = 0; x < stride; x++)
                            elements[x] = operation(x, y);
                    }
                });
            }
        }

        /// <summary>
        /// Apply operation to all elements
        /// </summary>
        public void ProcessElementWise(Func<float, float> operation)
        {
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* elements = &Elements[y, 0])
                    {
                        var stride = Stride;
                        for (var x = 0; x < stride; x++)
                            elements[x] = operation(elements[x]);
                    }
                });
            }
        }

        /// <summary>
        /// Apply operation to all elements (this,other)
        /// </summary>
        public void ProcessElementWise(FloatBitmap other, Func<float, float, float> operation)
        {
            unsafe
            {
                if (PlaneCount == other.PlaneCount)
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* elements = &Elements[y, 0])
                        fixed (float* otherElements = &other.Elements[y, 0])
                        {
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                                elements[x] = operation(elements[x], otherElements[x]);
                        }
                    });
                }
                else if (PlaneCount == 3 && other.PlaneCount == 1)
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* elements = &Elements[y, 0])
                        fixed (float* otherElements = &other.Elements[y, 0])
                        {
                            int xx = 0;
                            for (var x = 0; x < Width; x++)
                            {
                                var otherElement = otherElements[x];
                                elements[xx] = operation(elements[xx++], otherElement);
                                elements[xx] = operation(elements[xx++], otherElement);
                                elements[xx] = operation(elements[xx++], otherElement);
                            }
                        }
                    });
                }
                else
                    throw new InvalidOperationException("Unsupported number of planes " + PlaneCount);
            }
        }

        public void Add(float value)
        {
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* elements = &Elements[y, 0])
                    {
                        var stride = Stride;
                        for (var x = 0; x < stride; x++)
                            elements[x] += value;
                    }
                });
            }
        }

        public void Add(FloatBitmap other)
        {
            if (PlaneCount != other.PlaneCount)
                throw new InvalidOperationException("Unsupported number of planes");
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* elements = &Elements[y, 0])
                    fixed (float* otherElements = &other.Elements[y, 0])
                    {
                        var stride = Stride;
                        for (var x = 0; x < stride; x++)
                            elements[x] += otherElements[x];
                    }
                });
            }
        }

        /// <summary>
        /// this = value - this
        /// </summary>
        public void SubtractFrom(FloatBitmap value)
        {
            Debug.Assert(Stride == value.Stride);
            Debug.Assert(Height == value.Height);
            unsafe
            {
                var size = Stride * Height;
                if (size > 10000)
                {
                    Parallel.For(0, Height, y =>
                    {
                        fixed (float* otherElements = &value.Elements[y, 0])
                        fixed (float* elements = &Elements[y, 0])
                        {
                            var otherElement = otherElements;
                            var element = elements;
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                            {
                                *element = *otherElement - *element;
                                otherElement++;
                                element++;
                            }
                        }
                    });
                }
                else
                {
                    fixed (float* srcElements = value.Elements)
                    fixed (float* dstElements = Elements)
                    {
                        var src = srcElements;
                        var dst = dstElements;
                        for (var i = 0; i < size; i++)
                        {
                            *dst = *src - *dst;
                            src++;
                            dst++;
                        }
                    }
                }
            }
        }
    }
}