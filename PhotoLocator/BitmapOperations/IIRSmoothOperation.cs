using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public static class IIRSmoothOperation
    {
        public static void Apply(FloatBitmap image, double filterSize)
        {
            if (filterSize == 0)
                return;
            filterSize /= 4;
            var scale = 1 / (1 + filterSize);
            if (image.PlaneCount == 1)
                ApplyHorizontalSmoothSinglePlane(image, filterSize, scale);
            else
                ApplyHorizontalSmoothMultiplePlanesPlane(image, filterSize, scale);
            ApplyVerticalSmooth(image, (float)filterSize, (float)scale);
        }

        private static unsafe void ApplyHorizontalSmoothSinglePlane(FloatBitmap plane, double filterSize, double scale)
        {
            Parallel.For(0, plane.Height, y =>
            {
                var stride = plane.Stride;
                fixed (float* pixels = &plane.Elements[y, 0])
                {
                    var pix = pixels;
                    double value = *pix;
                    for (var x = stride; x > 1; x--)
                    {
                        pix++;
                        value = (value * filterSize + *pix) * scale;
                        *pix = (float)value;
                    }
                    for (var x = stride; x > 1; x--)
                    {
                        pix--;
                        value = (value * filterSize + *pix) * scale;
                        *pix = (float)value;
                    }
                }
            });
        }

        private static unsafe void ApplyHorizontalSmoothMultiplePlanesPlane(FloatBitmap plane, double filterSize, double scale)
        {
            Parallel.For(0, plane.Height, y =>
            {
                var width = plane.Width;
                var planeCount = plane.PlaneCount;
                for (int p = 0; p < planeCount; p++)
                    fixed (float* pixels = &plane.Elements[y, p])
                    {
                        var pix = pixels;
                        double value = *pix;
                        for (var x = width; x > 1; x--)
                        {
                            pix += planeCount;
                            value = (value * filterSize + *pix) * scale;
                            *pix = (float)value;
                        }
                        for (var x = width; x > 1; x--)
                        {
                            pix -= planeCount;
                            value = (value * filterSize + *pix) * scale;
                            *pix = (float)value;
                        }
                    }
            });
        }

        private static unsafe void ApplyVerticalSmooth(FloatBitmap plane, float filterSize, float scale)
        {
            var height = plane.Height;
            // Vectorized over columns when possible
            int vectorSize = Vector<float>.Count;
            int vectorizedSegments = plane.Stride / vectorSize;
            var filterSizeV = Vector.Create(filterSize);
            var scaleV = Vector.Create(scale);
            Parallel.For(0, vectorizedSegments, vi =>
            {
                int x = vi * vectorSize;
                var stride = plane.Stride;
                fixed (float* pixels = plane.Elements)
                {
                    float* colPtr = &pixels[x];
                    var v = Vector.Load(colPtr);
                    for (var y = height; y > 1; y--)
                    {
                        colPtr += stride;
                        var inV = Vector.Load(colPtr);
                        v = Vector.Multiply(Vector.Add(Vector.Multiply(v, filterSizeV), inV), scaleV);
                        v.Store(colPtr);
                    }
                    for (var y = height; y > 1; y--)
                    {
                        colPtr -= stride;
                        var inV = Vector.Load(colPtr);
                        v = Vector.Multiply(Vector.Add(Vector.Multiply(v, filterSizeV), inV), scaleV);
                        v.Store(colPtr);
                    }
                }
            });

            // Remaining columns - columns not divisible by vectorSize
            Parallel.For(vectorizedSegments * vectorSize, plane.Stride, x =>
            {
                var stride = plane.Stride;
                fixed (float* pixels = plane.Elements)
                {
                    var pix = &pixels[x];
                    var value = *pix;
                    for (var y = height; y > 1; y--)
                    {
                        pix += stride;
                        value = (value * filterSize + *pix) * scale;
                        *pix = value;
                    }
                    for (var y = height; y > 1; y--)
                    {
                        pix -= stride;
                        value = (value * filterSize + *pix) * scale;
                        *pix = value;
                    }
                }
            });
        }        
    }   
}
