using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public class BitmapPlaneInt16
    {
        public int Width
        {
            get; private set;
        }

        public int Height
        {
            get; private set;
        }

        public int NRows { get { return Height; } }

        public int NCols { get { return Width; } }

        /// <summary>
        /// Number of elements
        /// </summary>
        public int Size => Width * Height;

        public short[,] Elements => _elements;
        protected short[,] _elements = null!;

        public BitmapPlaneInt16(int width, int height)
        {
            New(width, height);
        }

        public BitmapPlaneInt16(FloatBitmap src, Func<float, short> remap)
        {
            Assign(src, remap);
        }

        public void Assign(FloatBitmap src, Func<float, short> remap)
        {
            New(src.Width, src.Height);
            unsafe
            {
                Parallel.For(0, Height, y =>
                {
                    fixed (float* srcPix = &src.Elements[y, 0])
                    fixed (short* dstPix = &Elements[y, 0])
                    {
                        for (var x = 0; x < Width; x++)
                            dstPix[x] = remap(srcPix[x]);
                    }
                });
            }
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }

        public void New(int width, int height)
        {
            if (width != Width || height != Height)
            {
                Width = width;
                Height = height;
                _elements = new short[Height, Width];
            }
        }

        public short this[int x, int y]
        {
            get { return _elements[y, x]; }
            set { _elements[y, x] = value; }
        }

        /// <summary>
        /// this = this + value
        /// </summary>
        public void Add(BitmapPlaneInt16 value)
        {
            Debug.Assert(NRows == value.NRows);
            Debug.Assert(NCols == value.NCols);
            unsafe
            {
                var size = Size;
                if (size > 10000)
                {
                    Parallel.For(0, NRows, r =>
                    {
                        fixed (short* otherElements = &value.Elements[r, 0])
                        fixed (short* elements = &Elements[r, 0])
                        {
                            var otherElement = otherElements;
                            var element = elements;
                            for (var i = 0; i < NCols; i++)
                            {
                                *element += *otherElement;
                                otherElement++;
                                element++;
                            }
                        }
                    });
                }
                else
                {
                    fixed (short* srcElements = value._elements)
                    fixed (short* dstElements = _elements)
                    {
                        var src = srcElements;
                        var dst = dstElements;
                        for (var i = 0; i < size; i++)
                        {
                            *dst += *src;
                            src++;
                            dst++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// this = value - this
        /// </summary>
        public void SubtractFrom(BitmapPlaneInt16 value)
        {
            Debug.Assert(NRows == value.NRows);
            Debug.Assert(NCols == value.NCols);
            unsafe
            {
                var size = Size;
                if (size > 10000)
                {
                    Parallel.For(0, NRows, r =>
                    {
                        fixed (short* otherElements = &value.Elements[r, 0])
                        fixed (short* elements = &Elements[r, 0])
                        {
                            var otherElement = otherElements;
                            var element = elements;
                            for (var i = 0; i < NCols; i++)
                            {
                                *element = (short)(*otherElement - *element);
                                otherElement++;
                                element++;
                            }
                        }
                    });
                }
                else
                {
                    fixed (short* srcElements = value._elements)
                    fixed (short* dstElements = _elements)
                    {
                        var src = srcElements;
                        var dst = dstElements;
                        for (var i = 0; i < size; i++)
                        {
                            *dst = (short)(*src - *dst);
                            src++;
                            dst++;
                        }
                    }
                }
            }
        }
    }
}
