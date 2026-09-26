using System;

namespace PhotoLocator.BitmapOperations
{
    class AstroStretchOperation : OperationBase
    {
        public double Stretch { get; set; } = 10;

        public double BackgroundSmooth { get; set; } = 2;

        public double BlackPoint { get; set; }

        public override void Apply()
        {
            if (SrcBitmap is not null && DstBitmap != SrcBitmap)
                DstBitmap.Assign(SrcBitmap);
            if (Stretch > 0)
            {
                var s = (float)Math.Exp(-Stretch);
                DstBitmap.ProcessElementWise(p => Math.Max(0, (s - 1) * p / ((2 * s - 1) * p - s)));
            }
            if (BackgroundSmooth > 0)
            {
                //var background = SplitBackgroundChannels ? new FloatBitmap(DstBitmap) : ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
                //IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth));
                //if (BlackPoint > 0)
                //{
                //    var bp = (float)BlackPoint;
                //    DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(p - b - bp, 0));
                //}
                //else
                //    DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(p - b, 0));

                //var background = ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
                //IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth));
                //var mask = ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
                //mask.ProcessElementWise(background, (p, b) => p - b > BlackPoint ? 1 : 0);
                //background = SplitBackgroundChannels ? new FloatBitmap(DstBitmap) : ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
                ////mask.SaveToFile("astroMask.jpg");
                //HoleClosingOperation.CloseHolesIteratively(background, mask, 0);
                //IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth));
                //DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(p - b, 0));

                const int BackgroundWidth = 128;
                var resizeOp = new LanczosResizeOperation { FilterFunc = LanczosResizeOperation.Lanczos2, FilterWindow = 1 };
                var backgroundReduced = resizeOp.Apply(DstBitmap, BackgroundWidth, (int)Math.Ceiling((double)BackgroundWidth / DstBitmap.Width * DstBitmap.Height));
                //backgroundReduced.SaveToFile("astroReducedBackground.jpg", FloatBitmap.DefaultMonitorGamma);

                var mask = ConvertToGrayscaleOperation.ConvertToGrayscale(backgroundReduced);
                var smoothBackground = new FloatBitmap(mask);
                IIRSmoothOperation.Apply(smoothBackground, BackgroundWidth * BackgroundSmooth);
                mask.ProcessElementWise(smoothBackground, (m, s) => m - s > 0 ? 1 : 0);

                //mask.SaveToFile("astroMask.jpg");

                HoleClosingOperation.CloseHolesIteratively(backgroundReduced, mask, 0);
                //backgroundReduced.SaveToFile("astroReducedBackgroundClosed.jpg", FloatBitmap.DefaultMonitorGamma);

                IIRSmoothOperation.Apply(backgroundReduced, BackgroundWidth * BackgroundSmooth);

                var background = resizeOp.Apply(backgroundReduced, DstBitmap.Width, DstBitmap.Height);
                //background.SaveToFile("astroBackgroundClosed.jpg", FloatBitmap.DefaultMonitorGamma);

                //IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth));

                var bp = (float)BlackPoint;
                DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(p - b - bp, 0));
            }
            else if (BlackPoint > 0)
            {
                var bp = (float)BlackPoint;
                DstBitmap.ProcessElementWise(p => Math.Max(p - bp, 0));
            }
        }

        public static double OptimizeStretch(FloatBitmap srcBitmap)
        {
            const double TargetMean = 0.1;
            const int SampleHeight = 100;

            var grayImage = ConvertToGrayscaleOperation.ConvertToGrayscale(srcBitmap);
            srcBitmap = new FloatBitmap(Math.Max(1, srcBitmap.Width * SampleHeight / srcBitmap.Height), SampleHeight, 1);
            BilinearResizeOperation.ApplyToPlaneParallel(grayImage, srcBitmap);  

            double bestStretch = 1;
            double bestMean = 0;
            var op = new AstroStretchOperation { SrcBitmap = srcBitmap, DstBitmap = new(), BackgroundSmooth = 0 };
            for (double s = 1; s <= 20; s += 0.2)
            {
                op.Stretch = s;
                op.Apply();
                var mean = op.DstBitmap.Mean();
                if (Math.Abs(mean - TargetMean) < Math.Abs(bestMean - TargetMean))
                {
                    bestStretch = s;
                    bestMean = mean;
                }
            }
            return bestStretch;
        }
    }
}
