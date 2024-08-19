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
                    Parallel.For(0, plane.Stride, x =>
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
                                if (prev < *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                            prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix -= width;
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
                    Parallel.For(0, plane.Stride, x =>
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
                                if (prev > *pix)
                                {
                                    prev = (prev * filterSize + *pix) * scale;
                                    *pix = prev;
                                }
                                else
                                    prev = *pix;
                            }
                            prev = *pix;
                            for (var y = 1; y < height; y++)
                            {
                                pix -= width;
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
                }
            }
        }
    }
}
