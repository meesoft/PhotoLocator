using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class MaxFramesOperation : CombineFramesOperationBase, IDisposable
    {
        public MaxFramesOperation(string? darkFramePath, bool enableRegistration, ROI? registrationRegion, CancellationToken ct)
            : base(darkFramePath, enableRegistration ? RegistrationMethod.BlackBorders : RegistrationMethod.None, registrationRegion, ct)
        {
        }

        public override void ProcessImage(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            Parallel.For(0, pixels.Length, i =>
            {
                if (pixels[i] > _accumulatorPixels![i])
                    _accumulatorPixels[i] = pixels[i];
            });
        }

        protected override double GetResultScaling()
        {
            return 1;
        }
    }
}
