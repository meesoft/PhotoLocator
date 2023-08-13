using PhotoLocator.Helpers;
using System;
using System.Threading.Tasks;

namespace PhotoLocator.PictureFileFormats
{
    public static class CMYK2RGBAConversionOperation8
    {
        public static void Apply(byte[] source, byte[] dest, int width, int height)
        {
            //bitmap.CopyPixels(rect, bytes, bytesPerPixel, 0);
            Parallel.For(0, height, yy =>
            {
                unsafe
                {
                    fixed (byte* src = &source[yy * width * 4])
                    fixed (byte* dst = &dest[yy * width * 4])
                    {
                        var p = 0;
                        for (int x = 0; x < width; x++)
                        {
                            float c = 255 - src[p + 0];
                            float m = 255 - src[p + 1];
                            float y = 255 - src[p + 2];
                            float k = (255 - src[p + 3]) / 255f;

                            float r = 80 + 0.5882f * c - 0.3529f * m - 0.1373f * y + 0.00185f * c * m + 0.00046f * y * c; // no YM
                            float g = 66 - 0.1961f * c + 0.2745f * m - 0.0627f * y + 0.00215f * c * m + 0.00008f * y * c + 0.00062f * y * m;
                            float b = 86 - 0.3255f * c - 0.1569f * m + 0.1647f * y + 0.00046f * c * m + 0.00123f * y * c + 0.00215f * y * m;

                            dst[p + 0] = (byte)RealMath.EnsureRange(b * k + 0.5f, 0, 255);
                            dst[p + 1] = (byte)RealMath.EnsureRange(g * k + 0.5f, 0, 255);
                            dst[p + 2] = (byte)RealMath.EnsureRange(r * k + 0.5f, 0, 255);
                            dst[p + 3] = 255;

                            p += 4;
                        }
                    }
                }
            });
        }
    }
}
