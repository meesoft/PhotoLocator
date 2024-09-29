using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    class BrightnessContrastOperation : OperationBase
    {
        /// <summary>
        /// 1 means no change
        /// </summary>
        public float Brightness = 1;

        /// <summary>
        /// 1 means no change
        /// </summary>
        public float Contrast = 1;

        public static void ApplyBrightness(FloatBitmap plane, float brightness)
        {
            if (brightness == 1)
                return;
            unsafe
            {
                Parallel.For(0, plane.Height, y =>
                {
                    fixed (float* pixels = &plane.Elements[y, 0])
                    {
                        var width = plane.Stride;
                        for (var x = 0; x < width; x++)
                        {
                            var pix = pixels[x];
                            if (pix > 0f && pix < 1f)
                            {
                                pix = 1f - (float)Math.Pow(1 - pix, brightness);
                                pixels[x] = pix;
                            }
                        }
                    }
                });
            }
        }

        public static void ApplyContrast(FloatBitmap plane, float contrast)
        {
            if (contrast == 1)
                return;
            unsafe
            {
                Parallel.For(0, plane.Height, y =>
                {
                    fixed (float* pixels = &plane.Elements[y, 0])
                    {
                        var width = plane.Stride;
                        for (var x = 0; x < width; x++)
                        {
                            var pix = pixels[x];
                            if (pix > 0f && pix < 1f)
                            {
                                if (pix <= 0.5f)
                                    pix = (float)Math.Pow(pix * 2, contrast) * 0.5f;
                                else
                                    pix = 1f - (float)Math.Pow((1 - pix) * 2, contrast) * 0.5f;
                                pixels[x] = pix;
                            }
                        }
                    }
                });
            }
        }

        public static void ApplyBrightnessContrast(FloatBitmap plane, float brightness, float contrast)
        {
            if (contrast == 1)
            {
                ApplyBrightness(plane, brightness);
                return;
            }
            if (brightness == 1)
            {
                ApplyContrast(plane, contrast);
                return;
            }
            unsafe
            {
                Parallel.For(0, plane.Height, y =>
                {
                    fixed (float* pixels = &plane.Elements[y, 0])
                    {
                        var width = plane.Stride;
                        for (var x = 0; x < width; x++)
                        {
                            var pix = pixels[x];
                            if (pix > 0f && pix < 1f)
                            {
                                if (pix <= 0.5f)
                                    pix = (float)Math.Pow(pix * 2, contrast) * 0.5f;
                                else
                                    pix = 1f - (float)Math.Pow((1f - pix) * 2, contrast) * 0.5f;
                                pix = 1f - (float)Math.Pow(1f - pix, brightness);
                                pixels[x] = pix;
                            }
                        }
                    }
                });
            }
        }

        public override void Apply()
        {
            if (DstBitmap != SrcBitmap && SrcBitmap != null)
                DstBitmap.Assign(SrcBitmap);
            ApplyBrightnessContrast(DstBitmap, Brightness, Contrast);
        }
    }
}
