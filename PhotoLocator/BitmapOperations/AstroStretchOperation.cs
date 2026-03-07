using System;

namespace PhotoLocator.BitmapOperations
{
    class AstroStretchOperation : OperationBase
    {
        public double Stretch { get; set; }

        public double BackgroundSmooth { get; set; }

        public double BackgroundMax { get; set; } = 0.25;

        public override void Apply()
        {
            if (DstBitmap != SrcBitmap)
                DstBitmap.Assign(SrcBitmap);
            var s = Math.Exp((1 - Stretch) * 15);
            DstBitmap.ProcessElementWise(p => (float)Math.Max(0, (s - 1) * p / ((2 * s - 1) * p - s)));

            var background = ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap);
            var max = (float)BackgroundMax;
            background.ProcessElementWise(p => Math.Min(p, max));
            IIRSmoothOperation.Apply(background, (float)Math.Exp(BackgroundSmooth * 5));

            DstBitmap.ProcessElementWise(background, (p, b) => Math.Max(0, p - b));
        }
    }
}
