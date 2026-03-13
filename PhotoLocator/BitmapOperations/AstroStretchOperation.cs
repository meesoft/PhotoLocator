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
    }
}
