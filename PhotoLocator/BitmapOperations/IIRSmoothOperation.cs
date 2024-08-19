using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public static class IIRSmoothOperation
    {
        public static void Apply(FloatBitmap plane, float filterSize)
        {
            if (filterSize == 0)
                return;
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
                Parallel.For(0, plane.Stride, x =>
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
