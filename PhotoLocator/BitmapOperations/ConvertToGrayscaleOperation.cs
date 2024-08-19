using PhotoLocator.Helpers;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    static class ConvertToGrayscaleOperation
    {
        public const float DefaultWeightR = 0.299f;
        public const float DefaultWeightG = 0.587f;
        public const float DefaultWeightB = 0.114f;

        /// <summary>
        /// Convert to grayscale plane using standard plane weights
        /// </summary>
        /// <param name="canReturnSource">If true and the source has only one plane then a reference to that plane is returned,
        /// otherwise a copy of the plane is returned</param>
        static public FloatBitmap ConvertToGrayscale(FloatBitmap bitmap, bool canReturnSource = false)
        {
            if (bitmap.PlaneCount == 1)
                return canReturnSource ? bitmap : new FloatBitmap(bitmap);
            if (bitmap.PlaneCount != 3)
                throw new UserMessageException("Unsupported number of planes: " + bitmap.PlaneCount);
            var grayPlane = new FloatBitmap(bitmap.Width, bitmap.Height, 1);
            unsafe
            {
                Parallel.For(0, grayPlane.Height, y =>
                {
                    fixed (float* src = &bitmap.Elements[y, 0])
                    fixed (float* dst = &grayPlane.Elements[y, 0])
                    {
                        float* srcPix = src;
                        int width = grayPlane.Width;
                        for (int x = 0; x < width; x++)
                        {
                            dst[x] = srcPix[0] * DefaultWeightR + srcPix[1] * DefaultWeightG + srcPix[2] * DefaultWeightB;
                            srcPix += 3;
                        }
                    }
                });
            }
            return grayPlane;
        }
    }
}
