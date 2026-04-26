using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Numerics;

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
            var height = plane.Height;
            unsafe
            {
                // Horizontal smooth
                Parallel.For(0, height, y =>
                {
                    var stride = plane.Stride;
                    fixed (float* pixels = plane.Elements)
                    {
                        var pix = &pixels[y * stride];
                        var value = *pix;
                        for (var x = stride; x > 1; x--)
                        {
                            pix++;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                        for (var x = stride; x > 1; x--)
                        {
                            pix--;
                            value = (value * filterSize + *pix) * scale;
                            *pix = value;
                        }
                    }
                });

                // Vertical smooth - vectorized over columns when possible
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
}
