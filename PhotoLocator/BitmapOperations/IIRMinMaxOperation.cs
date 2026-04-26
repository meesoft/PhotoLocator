using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public static class IIRMinMaxOperation
    {
        static public void MinFilter(FloatBitmap plane, float filterSize)
        {
            if (filterSize <= 0)
                return;
            filterSize /= 4f;
            var scale = 1f / (1f + filterSize);
            unsafe
            {
                for (var i = 0; i < 2; i++)
                {
                    // Horizontal
                    Parallel.For(0, plane.Height, y =>
                    {
                        var width = plane.Stride;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix = &pixels[y * plane.Stride];
                            var prev = *pix;
                            for (var x = 1; x < width; x++)
                            {
                                pix++;
                                if (prev < *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                            prev = *pix;
                            for (var x = 1; x < width; x++)
                            {
                                pix--;
                                if (prev < *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                        }
                    });
                    // Vertical
                    int quarterWidth = plane.Stride / 4;
                    Parallel.For(0, quarterWidth, x4 =>
                    {
                        int x = x4 * 4;
                        var width = plane.Stride;
                        var height = plane.Height;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix0 = &pixels[x];
                            var prev0 = *pix0;
                            var pix1 = pix0 + 1;
                            var prev1 = *pix1;
                            var pix2 = pix0 + 2;
                            var prev2 = *pix2;
                            var pix3 = pix0 + 3;
                            var prev3 = *pix3;
                            for (var y = 1; y < height; y++)
                            {
                                pix0 += width;
                                if (prev0 < *pix0) { prev0 = (prev0 * filterSize + *pix0) * scale; *pix0 = prev0; }
                                else prev0 = *pix0;
                                pix1 += width;
                                if (prev1 < *pix1) { prev1 = (prev1 * filterSize + *pix1) * scale; *pix1 = prev1; }
                                else prev1 = *pix1;
                                pix2 += width;
                                if (prev2 < *pix2) { prev2 = (prev2 * filterSize + *pix2) * scale; *pix2 = prev2; }
                                else prev2 = *pix2;
                                pix3 += width;
                                if (prev3 < *pix3) { prev3 = (prev3 * filterSize + *pix3) * scale; *pix3 = prev3; }
                                else prev3 = *pix3;
                            }
                            prev0 = *pix0;
                            prev1 = *pix1;
                            prev2 = *pix2;
                            prev3 = *pix3;
                            for (var y = 1; y < height; y++)
                            {
                                pix0 -= width;
                                if (prev0 < *pix0) { prev0 = (prev0 * filterSize + *pix0) * scale; *pix0 = prev0; }
                                else prev0 = *pix0;
                                pix1 -= width;
                                if (prev1 < *pix1) { prev1 = (prev1 * filterSize + *pix1) * scale; *pix1 = prev1; }
                                else prev1 = *pix1;
                                pix2 -= width;
                                if (prev2 < *pix2) { prev2 = (prev2 * filterSize + *pix2) * scale; *pix2 = prev2; }
                                else prev2 = *pix2;
                                pix3 -= width;
                                if (prev3 < *pix3) { prev3 = (prev3 * filterSize + *pix3) * scale; *pix3 = prev3; }
                                else prev3 = *pix3;
                            }
                        }
                    });
                    Parallel.For(quarterWidth * 4, plane.Stride, x =>
                    {
                        var width = plane.Stride;
                        var height = plane.Height;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix = &pixels[x];
                            var prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix += width;
                                if (prev < *pix) { prev = (prev * filterSize + *pix) * scale; *pix = prev; }
                                else prev = *pix;
                            }
                            prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix -= width;
                                if (prev < *pix) { prev = (prev * filterSize + *pix) * scale; *pix = prev; }
                                else prev = *pix;
                            }
                        }
                    });
                }
            }
        }

        static public void MaxFilter(FloatBitmap plane, float filterSize)
        {
            if (filterSize <= 0)
                return;
            filterSize /= 4f;
            var scale = 1f / (1f + filterSize);
            unsafe
            {
                for (var i = 0; i < 2; i++)
                {
                    // Horizontal
                    Parallel.For(0, plane.Height, y =>
                    {
                        var width = plane.Stride;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix = &pixels[y * plane.Stride];
                            var prev = *pix;
                            for (var x = 1; x < width; x++)
                            {
                                pix++;
                                if (prev > *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                            prev = *pix;
                            for (var x = 1; x < width; x++)
                            {
                                pix--;
                                if (prev > *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                        }
                    });
                    // Vertical
                    int quarterWidth = plane.Stride / 4;
                    Parallel.For(0, quarterWidth, x4 =>
                    {
                        int x = x4 * 4;
                        var width = plane.Stride;
                        var height = plane.Height;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix0 = &pixels[x];
                            var prev0 = *pix0;
                            var pix1 = pix0 + 1;
                            var prev1 = *pix1;
                            var pix2 = pix0 + 2;
                            var prev2 = *pix2;
                            var pix3 = pix0 + 3;
                            var prev3 = *pix3;
                            for (var y = 1; y < height; y++)
                            {
                                pix0 += width;
                                if (prev0 > *pix0) { prev0 = (prev0 * filterSize + *pix0) * scale; *pix0 = prev0; }
                                else prev0 = *pix0;
                                pix1 += width;
                                if (prev1 > *pix1) { prev1 = (prev1 * filterSize + *pix1) * scale; *pix1 = prev1; }
                                else prev1 = *pix1;
                                pix2 += width;
                                if (prev2 > *pix2) { prev2 = (prev2 * filterSize + *pix2) * scale; *pix2 = prev2; }
                                else prev2 = *pix2;
                                pix3 += width;
                                if (prev3 > *pix3) { prev3 = (prev3 * filterSize + *pix3) * scale; *pix3 = prev3; }
                                else prev3 = *pix3;
                            }
                            prev0 = *pix0;
                            prev1 = *pix1;
                            prev2 = *pix2;
                            prev3 = *pix3;
                            for (var y = 1; y < height; y++)
                            {
                                pix0 -= width;
                                if (prev0 > *pix0) { prev0 = (prev0 * filterSize + *pix0) * scale; *pix0 = prev0; }
                                else prev0 = *pix0;
                                pix1 -= width;
                                if (prev1 > *pix1) { prev1 = (prev1 * filterSize + *pix1) * scale; *pix1 = prev1; }
                                else prev1 = *pix1;
                                pix2 -= width;
                                if (prev2 > *pix2) { prev2 = (prev2 * filterSize + *pix2) * scale; *pix2 = prev2; }
                                else prev2 = *pix2;
                                pix3 -= width;
                                if (prev3 > *pix3) { prev3 = (prev3 * filterSize + *pix3) * scale; *pix3 = prev3; }
                                else prev3 = *pix3;
                            }
                        }
                    });
                    Parallel.For(quarterWidth * 4, plane.Stride, x =>
                    {
                        var width = plane.Stride;
                        var height = plane.Height;
                        fixed (float* pixels = plane.Elements)
                        {
                            var pix = &pixels[x];
                            var prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix += width;
                                if (prev > *pix) { prev = (prev * filterSize + *pix) * scale; *pix = prev; }
                                else prev = *pix;
                            }
                            prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix -= width;
                                if (prev > *pix) { prev = (prev * filterSize + *pix) * scale; *pix = prev; }
                                else prev = *pix;
                            }
                        }
                    });
                }
            }
        }
    }
}
