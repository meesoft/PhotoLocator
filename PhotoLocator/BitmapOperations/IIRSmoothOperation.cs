using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
                    // Prefer hardware intrinsics if available (AVX/ SSE). Fallback to scalar per-column processing.
                    bool hasAvx = Avx.IsSupported;
                    bool hasSse = Sse.IsSupported;
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
                                Avx.Store(colPtr, v);
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
                    else if (hasSse)
                    {
                        var filterSizeV = Vector128.Create(filterSize);
                        var scaleV = Vector128.Create(scale);
                        Parallel.For(0, vectorizedSegments, vi =>
                        {
                            int x = vi * 4;
                            var stride = plane.Stride;
                            fixed (float* pixels = plane.Elements)
                            {
                                float* colPtr = &pixels[x];
                                var v = Sse.LoadVector128(colPtr);
                                Sse.Store(colPtr, v);
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr += stride;
                                    var inV = Sse.LoadVector128(colPtr);
                                    v = Sse.Multiply(Sse.Add(Sse.Multiply(v, filterSizeV), inV), scaleV);
                                    Sse.Store(colPtr, v);
                                }
                                for (var y = height; y > 1; y--)
                                {
                                    colPtr -= stride;
                                    var inV = Sse.LoadVector128(colPtr);
                                    v = Sse.Multiply(Sse.Add(Sse.Multiply(v, filterSizeV), inV), scaleV);
                                    Sse.Store(colPtr, v);
                                }
                            }
                        });
                    }
                    else
                    {
                        Parallel.For(0, vectorizedSegments, vi =>
                        {
                            int x = vi * 4;
                            var stride = plane.Stride;
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
                                for (var y = height; y > 1; y--)
                                {
                                    pix0 += stride;
                                    value0 = (value0 * filterSize + *pix0) * scale;
                                    *pix0 = value0;
                                    pix1 += stride;
                                    value1 = (value1 * filterSize + *pix1) * scale;
                                    *pix1 = value1;
                                    pix2 += stride;
                                    value2 = (value2 * filterSize + *pix2) * scale;
                                    *pix2 = value2;
                                    pix3 += stride;
                                    value3 = (value3 * filterSize + *pix3) * scale;
                                    *pix3 = value3;
                                }
                                for (var y = height; y > 1; y--)
                                {
                                    pix0 -= stride;
                                    value0 = (value0 * filterSize + *pix0) * scale;
                                    *pix0 = value0;
                                    pix1 -= stride;
                                    value1 = (value1 * filterSize + *pix1) * scale;
                                    *pix1 = value1;
                                    pix2 -= stride;
                                    value2 = (value2 * filterSize + *pix2) * scale;
                                    *pix2 = value2;
                                    pix3 -= stride;
                                    value3 = (value3 * filterSize + *pix3) * scale;
                                    *pix3 = value3;
                                }
                            }
                        });
                    }
                    // Remaining columns (scalar) - includes columns not divisible by vectorSize
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
