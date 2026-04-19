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
                unsafe
                {
                    bool hasAvx = Avx.IsSupported;
                    int vectorSize = hasAvx ? 8 : 4;
                    int vectorizedSegments = plane.Stride / vectorSize;

                    if (hasAvx)
                    {
                        var filterSizeV = Vector256.Create(filterSize);
                        var scaleV = Vector256.Create(scale);
                        Parallel.For(0, vectorizedSegments, vi =>
                        {
                            int x = vi * 8;
                            var stride = plane.Stride;
                            fixed (float* pixels = plane.Elements)
                            {
                                float* colPtr = &pixels[x];
                                var v = Avx.LoadVector256(colPtr);
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr += stride;
                                    var inV = Avx.LoadVector256(colPtr);
                                    v = Avx.Multiply(Avx.Add(Avx.Multiply(v, filterSizeV), inV), scaleV);
                                    Avx.Store(colPtr, v);
                                }
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr -= stride;
                                    var inV = Avx.LoadVector256(colPtr);
                                    v = Avx.Multiply(Avx.Add(Avx.Multiply(v, filterSizeV), inV), scaleV);
                                    Avx.Store(colPtr, v);
                                }
                            }
                        });
                    }
                    else
                    {
                        var filterSizeV = Vector4.Create(filterSize);
                        var scaleV = Vector4.Create(scale);
                        Parallel.For(0, vectorizedSegments, vi =>
                        {
                            int x = vi * 4;
                            var stride = plane.Stride;
                            fixed (float* pixels = plane.Elements)
                            {
                                float* colPtr = &pixels[x];
                                int vectorizedSegments = plane.Stride / vectorSize;
                                var v = Vector4.Load(colPtr);
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr += stride;
                                    var inV = Vector4.Load(colPtr);
                                    v = Vector4.Multiply(Vector4.Add(Vector4.Multiply(v, filterSizeV), inV), scaleV);
                                    v.Store(colPtr);
                                }
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr -= stride;
                                    var inV = Vector4.Load(colPtr);
                                    v = Vector4.Multiply(Vector4.Add(Vector4.Multiply(v, filterSizeV), inV), scaleV);
                                    v.Store(colPtr);
                                }
                            }
                        });
                    }
                    
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
}
