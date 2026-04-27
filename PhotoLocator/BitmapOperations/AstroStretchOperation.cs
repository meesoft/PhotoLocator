using System;

namespace PhotoLocator.BitmapOperations
{
    class AstroStretchOperation : OperationBase
    {
        public double Stretch { get; set; } = 10;

        public double BackgroundSmooth { get; set; } = 8;

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
                var background = ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
                IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth));
                DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(p - b, 0));
            }
        }

        public static double OptimizeStretch(FloatBitmap srcBitmap)
        {
            const double TargetMean = 0.1;
            const int SampleHeight = 100;

            var grayImage = ConvertToGrayscaleOperation.ConvertToGrayscale(srcBitmap);
            srcBitmap = new FloatBitmap(srcBitmap.Width * SampleHeight / srcBitmap.Height, SampleHeight, 1);
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
