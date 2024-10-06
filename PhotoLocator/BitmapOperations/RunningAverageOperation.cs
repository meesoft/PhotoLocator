using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class RunningAverageOperation : CombineFramesOperationBase
    {
        readonly int _numberOfFramesToAverage;
        readonly Queue<byte[]> _previousFrames;

        public RunningAverageOperation(int numberOfFramesToAverage, string? darkFramePath, CancellationToken ct)
            : base(null, RegistrationMethod.None, null, ct)
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
