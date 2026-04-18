using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public static class IIRSmoothOperation
    {
        public static void Apply(FloatBitmap plane, float filterSize)
        {
            if (filterSize == 0)
                return;
            Debug.Assert(plane.PlaneCount == 1);
            filterSize /= 4f;
            var scale = 1f / (1f + filterSize);
            unsafe
            {
                // Horizontal smooth
                Parallel.For(0, plane.Height, y =>
                {
                    var width = plane.Stride;
                    fixed (float* pixels = plane.Elements)
                    {
                        var pix = &pixels[y * width];
                        var value = *pix;
                        for (var x = width; x > 1; x--)
                        {
                            pix++;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                        for (var x = width; x > 1; x--)
                        {
                            pix--;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                    }
                });
                // Vertical smooth
                int quaterWidth = plane.Stride / 4;
                Parallel.For(0, quaterWidth, x4 =>
                {
                    int x = x4 * 4;
                    var width = plane.Stride;
                    fixed (float* pixels = plane.Elements)
                    {
                        var pix0 = &pixels[x];
                        var value0 = *pix0;
                        var pix1 = pix0 + 1;
                        var value1 = *pix1;
                        var pix2 = pix0 + 2;
                        var value2 = *pix2;
                        var pix3 = pix0 + 3;
                        var value3 = *pix3;
                        for (var y = plane.Height; y > 1; y--)
                        {
                            pix0 += width;
                            value0 = (value0 * filterSize + *pix0) * scale;
                            *pix0 = value0;
                            pix1 += width;
                            value1 = (value1 * filterSize + *pix1) * scale;
                            *pix1 = value1;
                            pix2 += width;
                            value2 = (value2 * filterSize + *pix2) * scale;
                            *pix2 = value2;
                            pix3 += width;
                            value3 = (value3 * filterSize + *pix3) * scale;
                            *pix3 = value3;
                        }
                        for (var y = plane.Height; y > 1; y--)
                        {
                            pix0 -= width;
                            value0 = (value0 * filterSize + *pix0) * scale;
                            *pix0 = value0;
                            pix1 -= width;
                            value1 = (value1 * filterSize + *pix1) * scale;
                            *pix1 = value1;
                            pix2 -= width;
                            value2 = (value2 * filterSize + *pix2) * scale;
                            *pix2 = value2;
                            pix3 -= width;
                            value3 = (value3 * filterSize + *pix3) * scale;
                            *pix3 = value3;
                        }
                    }
                });
                Parallel.For(quaterWidth * 4, plane.Stride, x =>
                {
                    var width = plane.Stride;
                    fixed (float* pixels = plane.Elements)
                    {
                        var pix = &pixels[x];
                        var value = *pix;
                        for (var y = plane.Height; y > 1; y--)
                        {
                            pix += width;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                        for (var y = plane.Height; y > 1; y--)
                        {
                            pix -= width;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                    }
                });
            }
        }
    }   
}
