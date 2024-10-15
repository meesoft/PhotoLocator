using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    static class ResizeBilinearOperation
    {
        /// <summary>
        /// Determine source ROI given destination ROI
        /// </summary>
        public static ROI GetSourceROI(FloatBitmap src, FloatBitmap dst, in ROI dstROI)
        {
            float scaleX = 0, scaleY = 0;
            if (dst.Width > 1)
                scaleX = (float)(src.Width - 1) / (dst.Width - 1);
            if (dst.Height > 1)
                scaleY = (float)(src.Height - 1) / (dst.Height - 1);
            return new ROI(
                (int)(dstROI.Left * scaleX),
                Math.Min((int)(dstROI.Right * scaleX + 0.01f) + 1, src.Width - 1),
                (int)(dstROI.Top * scaleY),
                Math.Min((int)(dstROI.Bottom * scaleY + 0.01f) + 1, src.Height - 1));
        }

        /// <summary>
        /// Determine source ROI given destination ROI
        /// </summary>
        public static ROI GetSourceROI(BitmapPlaneInt16 src, BitmapPlaneInt16 dst, in ROI dstROI)
        {
            float scaleX = 0, scaleY = 0;
            if (dst.Width > 1)
                scaleX = (float)(src.Width - 1) / (dst.Width - 1);
            if (dst.Height > 1)
                scaleY = (float)(src.Height - 1) / (dst.Height - 1);
            return new ROI(
                (int)(dstROI.Left * scaleX),
                Math.Min((int)(dstROI.Right * scaleX + 0.01f) + 1, src.Width - 1),
                (int)(dstROI.Top * scaleY),
                Math.Min((int)(dstROI.Bottom * scaleY + 0.01f) + 1, src.Height - 1));
        }

        /// <summary>
        /// Scale bitmap plane given the destination region of interest
        /// </summary>
        public static void ApplyToPlane(FloatBitmap srcPlane, FloatBitmap dstPlane, ROI dstROI)
        {
            Debug.Assert(dstROI.Right <= dstPlane.Width && dstROI.Bottom <= dstPlane.Height, "Invalid ROI");
            float scaleX = 0, scaleY = 0;
            if (dstPlane.Width > 1)
                scaleX = (float)(srcPlane.Width - 1) / (dstPlane.Width - 1);
            if (dstPlane.Height > 1)
                scaleY = (float)(srcPlane.Height - 1) / (dstPlane.Height - 1);
            int srcWidth = srcPlane.Width;
            unsafe
            {
                fixed (float* srcPixels = srcPlane.Elements)
                fixed (float* dstPixels = dstPlane.Elements)
                {
                    for (var dstY = dstROI.Top; dstY <= dstROI.Bottom; dstY++)
                    {
                        var dstPix = &dstPixels[dstY * dstPlane.Width + dstROI.Left];
                        var srcY = dstY * scaleY;
                        var intY = (int)srcY;
                        var fracY = srcY - intY;

                        var srcLine = &srcPixels[intY * srcWidth];
                        var srcNext = srcLine;
                        if (intY < srcPlane.Height - 1)
                            srcNext = &srcNext[srcWidth];

                        var srcX = dstROI.Left * scaleX;
                        for (var dstX = dstROI.Left; dstX <= dstROI.Right; dstX++)
                        {
                            var intX = (int)srcX;
                            if (intX < srcWidth - 1)
                            {
                                var fracX = srcX - intX;
                                *dstPix = (srcLine[intX] * (1 - fracX) + srcLine[intX + 1] * fracX) * (1 - fracY) +
                                          (srcNext[intX] * (1 - fracX) + srcNext[intX + 1] * fracX) * fracY;
                                //var s00 = srcLine[intX]; var s10 = srcLine[intX + 1];
                                //var l0 = (s00 + (-s00 + s10) * fracX);
                                //var s01 = srcNext[intX]; var s11 = srcNext[intX + 1];
                                //var l1 = (s01 + (-s01 + s11) * fracX);
                                //*dstPix = l0 + (-l0 + l1) * fracY;
                                Debug.Assert(!float.IsNaN(*dstPix));
                            }
                            else // Last pixel in line
                            {
                                *dstPix = srcLine[intX] * (1 - fracY) +
                                          srcNext[intX] * fracY;
                                //var l0 = srcLine[intX];
                                //var l1 = srcNext[intX];
                                //*dstPix = l0 + (-l0 + l1) * fracY;
                                Debug.Assert(!float.IsNaN(*dstPix));
                            }
                            dstPix++;
                            srcX += scaleX;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Scale bitmap plane given the destination region of interest
        /// </summary>
        public static void ApplyToPlane(BitmapPlaneInt16 srcPlane, BitmapPlaneInt16 dstPlane, ROI dstROI)
        {
            Debug.Assert(dstROI.Right <= dstPlane.Width && dstROI.Bottom <= dstPlane.Height, "Invalid ROI");
            float scaleX = 0, scaleY = 0;
            if (dstPlane.Width > 1)
                scaleX = (float)(srcPlane.Width - 1) / (dstPlane.Width - 1);
            if (dstPlane.Height > 1)
                scaleY = (float)(srcPlane.Height - 1) / (dstPlane.Height - 1);
            int srcWidth = srcPlane.Width;
            unsafe
            {
                fixed (short* srcPixels = srcPlane.Elements)
                fixed (short* dstPixels = dstPlane.Elements)
                {
                    for (var dstY = dstROI.Top; dstY <= dstROI.Bottom; dstY++)
                    {
                        var dstPix = &dstPixels[dstY * dstPlane.Width + dstROI.Left];
                        var srcY = dstY * scaleY;
                        var intY = (int)srcY;
                        var fracY = srcY - intY;
                        //int fracYi = (int)(fracY * 256 + 0.5f);

                        var srcLine = &srcPixels[intY * srcWidth];
                        var srcNext = srcLine;
                        if (intY < srcPlane.Height - 1)
                            srcNext = &srcNext[srcWidth];

                        var srcX = dstROI.Left * scaleX;
                        for (var dstX = dstROI.Left; dstX <= dstROI.Right; dstX++)
                        {
                            var intX = (int)srcX;
                            if (intX < srcWidth - 1)
                            {
                                var fracX = srcX - intX;
                                *dstPix = (short)((srcLine[intX] * (1 - fracX) + srcLine[intX + 1] * fracX) * (1 - fracY) +
                                                  (srcNext[intX] * (1 - fracX) + srcNext[intX + 1] * fracX) * fracY + 0.5f);
                                //int fracXi = (int)(fracX * 256 + 0.5f);
                                //* dstPix = (short)(((srcLine[intX] * (256 - fracXi) + srcLine[intX + 1] * (fracXi)) * (256 - fracYi) +
                                //                    (srcNext[intX] * (256 - fracXi) + srcNext[intX + 1] * (fracXi)) * (fracYi) + 32768) >> 16);
                            }
                            else // Last pixel in line
                            {
                                *dstPix = (short)(srcLine[intX] * (1 - fracY) +
                                                  srcNext[intX] * fracY + 0.5f);
                                /**dstPix = (short)((srcLine[intX] * (256 - fracYi) +
                                                   srcNext[intX] * (fracYi) + 128) >> 8);*/
                            }
                            dstPix++;
                            srcX += scaleX;
                        }
                    }
                }
            }
        }

        public static void ApplyToPlaneParallel(FloatBitmap srcPlane, FloatBitmap dstPlane, ROI dstROI)
        {
            Debug.Assert(dstROI.Right <= dstPlane.Width && dstROI.Bottom <= dstPlane.Height, "Invalid ROI");
            float scaleX = 0, scaleY = 0;
            if (dstPlane.Width > 1)
                scaleX = (float)(srcPlane.Width - 1) / (dstPlane.Width - 1);
            if (dstPlane.Height > 1)
                scaleY = (float)(srcPlane.Height - 1) / (dstPlane.Height - 1);
            unsafe
            {
                Parallel.For(dstROI.Top, dstROI.Bottom + 1, dstY =>
                {
                    int srcWidth = srcPlane.Width;
                    fixed (float* srcPixels = srcPlane.Elements)
                    fixed (float* dstPixels = dstPlane.Elements)
                    {
                        var dstPix = &dstPixels[dstY * dstPlane.Width + dstROI.Left];
                        var srcY = dstY * scaleY;
                        var intY = (int)srcY;
                        var fracY = srcY - intY;

                        var srcLine = &srcPixels[intY * srcWidth];
                        var srcNext = srcLine;
                        if (intY < srcPlane.Height - 1)
                            srcNext = &srcNext[srcWidth];

                        var srcX = dstROI.Left * scaleX;
                        for (var dstX = dstROI.Left; dstX <= dstROI.Right; dstX++)
                        {
                            var intX = (int)srcX;
                            if (intX < srcWidth - 1)
                            {
                                var fracX = srcX - intX;
                                *dstPix = (srcLine[intX] * (1 - fracX) + srcLine[intX + 1] * fracX) * (1 - fracY) +
                                          (srcNext[intX] * (1 - fracX) + srcNext[intX + 1] * fracX) * fracY;
                                Debug.Assert(!float.IsNaN(*dstPix));
                            }
                            else // Last pixel in line
                            {
                                *dstPix = srcLine[intX] * (1 - fracY) +
                                          srcNext[intX] * fracY;
                                Debug.Assert(!float.IsNaN(*dstPix));
                            }
                            dstPix++;
                            srcX += scaleX;
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Scale bitmap plane given the destination region of interest
        /// </summary>
        public static void ApplyToPlaneParallel(BitmapPlaneInt16 srcPlane, BitmapPlaneInt16 dstPlane, ROI dstROI)
        {
            Debug.Assert(dstROI.Right <= dstPlane.Width && dstROI.Bottom <= dstPlane.Height, "Invalid ROI");
            float scaleX = 0, scaleY = 0;
            if (dstPlane.Width > 1)
                scaleX = (float)(srcPlane.Width - 1) / (dstPlane.Width - 1);
            if (dstPlane.Height > 1)
                scaleY = (float)(srcPlane.Height - 1) / (dstPlane.Height - 1);
            unsafe
            {
                Parallel.For(dstROI.Top, dstROI.Bottom + 1, dstY =>
                {
                    int srcWidth = srcPlane.Width;
                    fixed (short* srcPixels = srcPlane.Elements)
                    fixed (short* dstPixels = dstPlane.Elements)
                    {
                        var dstPix = &dstPixels[dstY * dstPlane.Width + dstROI.Left];
                        var srcY = dstY * scaleY;
                        var intY = (int)srcY;
                        var fracY = srcY - intY;

                        var srcLine = &srcPixels[intY * srcWidth];
                        var srcNext = srcLine;
                        if (intY < srcPlane.Height - 1)
                            srcNext = &srcNext[srcWidth];

                        var srcX = dstROI.Left * scaleX;
                        for (var dstX = dstROI.Left; dstX <= dstROI.Right; dstX++)
                        {
                            var intX = (int)srcX;
                            if (intX < srcWidth - 1)
                            {
                                var fracX = srcX - intX;
                                *dstPix = (short)((srcLine[intX] * (1 - fracX) + srcLine[intX + 1] * fracX) * (1 - fracY) +
                                                  (srcNext[intX] * (1 - fracX) + srcNext[intX + 1] * fracX) * fracY + 0.5f);
                            }
                            else // Last pixel in line
                            {
                                *dstPix = (short)(srcLine[intX] * (1 - fracY) +
                                                  srcNext[intX] * fracY + 0.5f);
                            }
                            dstPix++;
                            srcX += scaleX;
                        }
                    }
                });
            }
        }

        public static void ApplyToPlaneParallel(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            ApplyToPlaneParallel(srcPlane, dstPlane, new ROI(0, dstPlane.Width - 1, 0, dstPlane.Height - 1));
        }

        public static void ApplyToPlaneParallel(BitmapPlaneInt16 srcPlane, BitmapPlaneInt16 dstPlane)
        {
            ApplyToPlaneParallel(srcPlane, dstPlane, new ROI(0, dstPlane.Width - 1, 0, dstPlane.Height - 1));
        }
    }
}
