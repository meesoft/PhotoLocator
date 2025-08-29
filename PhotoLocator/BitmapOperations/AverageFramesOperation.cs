using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class AverageFramesOperation : CombineFramesOperationBase
    {
        public AverageFramesOperation(string? darkFramePath, CombineFramesRegistration? registrationSettings, CancellationToken ct)
            : base(darkFramePath, registrationSettings?.ToCombineFramesRegistrationFull(RegistrationOperation.Borders.Mirror), ct)
        {
        }

        public override void ProcessImage(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            Parallel.For(0, pixels.Length, i => _accumulatorPixels![i] += pixels[i]);
        }

        protected override double GetResultScaling()
        {
            return 1.0 / ProcessedImages;
        }
    }
}
