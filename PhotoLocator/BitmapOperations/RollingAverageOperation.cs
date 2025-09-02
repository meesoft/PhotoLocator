using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class RollingAverageOperation : CombineFramesOperationBase
    {
        readonly int _numberOfFramesToAverage;
        readonly Queue<byte[]> _previousFrames;

        public RollingAverageOperation(int numberOfFramesToAverage, string? darkFramePath, CombineFramesRegistration? registrationSettings, CancellationToken ct)
            : base(darkFramePath, registrationSettings?.ToCombineFramesRegistrationBase(RegistrationOperation.Borders.Mirror), ct)
        {
            _numberOfFramesToAverage = numberOfFramesToAverage;
            _previousFrames = new Queue<byte[]>(_numberOfFramesToAverage);
        }

        public override void ProcessImage(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            if (_previousFrames.Count >= _numberOfFramesToAverage)
            {
                var oldest = _previousFrames.Dequeue();
                Parallel.For(0, oldest.Length, i => _accumulatorPixels![i] -= oldest[i]);
            }
            Parallel.For(0, pixels.Length, i => _accumulatorPixels![i] += pixels[i]);
            _previousFrames.Enqueue(pixels);
        }
        
        protected override double GetResultScaling()
        {
            return 1.0 / _previousFrames.Count;
        }
    }
}
