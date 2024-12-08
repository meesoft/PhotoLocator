using PhotoLocator.Helpers;
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

        public float[,] Elements => _elements;
        float[,] _elements = null!;

        public float this[int x, int y]
        {
            get { return _elements[y, x]; }
            set { _elements[y, x] = value; }
        }

        /// <summary> Gray, RGB or CMYK </summary>
        public int PlaneCount { get; private set; }

        public int Stride => _elements.GetLength(1);

        public int Width { get; private set; }

        public int Height => _elements.GetLength(0);

        public int Size => Stride * Height;

        public override string ToString()
        {
            return Stride + " x " + Height;
        }

        public void New(int width, int height, int planes)
        {
            var stride = width * planes;
            if (_elements == null || stride != Stride || height != Height)
                _elements = new float[height, stride];
            Width = width;
            PlaneCount = planes;
        }

        public void Assign(FloatBitmap source)
        {
            New(source.Width, source.Height, source.PlaneCount);
            Array.Copy(source._elements, _elements, Stride * Height);
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (ushort* sourceRow = &sourcePixels[y * Stride])
                        {
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                                elements[x] = gamma[sourceRow[x]];
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (byte* sourceRow = &sourcePixels[y * Stride])
                        {
                            for (var x = 0; x < Width; x++)
                            {
                                elements[x * 3 + 2] = gamma[sourceRow[x * 3 + 0]];
                                elements[x * 3 + 1] = gamma[sourceRow[x * 3 + 1]];
                                elements[x * 3 + 0] = gamma[sourceRow[x * 3 + 2]];
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (byte* sourceRow = &sourcePixels[y * Width * 4])
                        {
                            for (var x = 0; x < Width; x++)
                            {
                                elements[x * 3 + 2] = gamma[sourceRow[x * 4 + 0]];
                                elements[x * 3 + 1] = gamma[sourceRow[x * 4 + 1]];
                                elements[x * 3 + 0] = gamma[sourceRow[x * 4 + 2]];
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (byte* sourceRow = &sourcePixels[y * Stride])
                        {
                            var stride = Stride;
                            for (var x = 0; x < stride; x++)
                                elements[x] = gamma[sourceRow[x]];
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
                        for (int x = 0; x < Width; x++)
                            dstPix[x] = remap(srcPix[x]);
                    }
                });
            }
        }

        public BitmapSource ToBitmapSource(double dpiX, double dpiY, double gamma, PixelFormat? format = null)
        {
            if (!format.HasValue)
                format = PlaneCount switch
                {
                    1 => PixelFormats.Gray8,
                    3 => PixelFormats.Rgb24,
                    4 => PixelFormats.Cmyk32,
                    _ => throw new UserMessageException("Unsupported number of planes: " + PlaneCount)
                };
            var pixels = ArrayPool<byte>.Shared.Rent(Height * Stride);
            var gammaLut = CreateGammaLookupFloatToByte(gamma);
            unsafe
            {
                //gamma = 1 / gamma;
                Parallel.For(0, Height, y =>
                {
                    fixed (byte* gamma = gammaLut)
                    fixed (float* elements = &_elements[y, 0])
                    fixed (byte* destRow = &pixels[y * Stride])
                    {
                        var stride = Stride;
                        for (var x = 0; x < stride; x++)
                            //destRow[x] = (byte)IntMath.Clamp((int)(Math.Pow(elements[x], gamma) * 255 + 0.5), 0, 255);
                            destRow[x] = gamma[IntMath.Clamp((int)(elements[x] * FloatToByteGammaLutRange + 0.5f), 0, FloatToByteGammaLutRange)];
                    }
                });
            }
            ArrayPool<byte>.Shared.Return(gammaLut);
            var result = BitmapSource.Create(Width, Height, dpiX, dpiY, format.Value, null, pixels, Stride);
            result.Freeze();
            ArrayPool<byte>.Shared.Return(pixels);
            return result;
        }

        static float[] CreateDeGammaLookup(double gamma, int range)
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
            return _elements[y, x];
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
                fixed (float* elements = _elements)
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
                fixed (float* elements = _elements)
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
                fixed (float* elements = _elements)
                {
                    var size = Size;
                    for (var i = 0; i < size; i++)
                    {
                        min = Math.Min(min, elements[i]);
                        max = Math.Max(max, elements[i]);
                    }
                }
            }
            return (min, max);
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
                    fixed (float* elements = &_elements[y, 0])
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
                    fixed (float* elements = &_elements[y, 0])
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (float* otherElements = &other._elements[y, 0])
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
                        fixed (float* elements = &_elements[y, 0])
                        fixed (float* otherElements = &other._elements[y, 0])
                        {
                            int xx = 0;
                            for (var x = 0; x < Width; x++)
                            {
                                elements[xx] = operation(elements[xx++], otherElements[x]);
                                elements[xx] = operation(elements[xx++], otherElements[x]);
                                elements[xx] = operation(elements[xx++], otherElements[x]);
                            }
                        }
                    });
                }
                else
                    throw new InvalidOperationException("Unsupported number of planes");
            }
        }

        public void Add(float value)
        {
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* elements = &_elements[y, 0])
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
                    fixed (float* elements = &_elements[y, 0])
                    fixed (float* otherElements = &other._elements[y, 0])
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
                    fixed (float* srcElements = value._elements)
                    fixed (float* dstElements = _elements)
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