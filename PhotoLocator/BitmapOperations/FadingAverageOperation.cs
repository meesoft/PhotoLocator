using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class FadingAverageOperation : CombineFramesOperationBase
    {
        readonly int _numberOfFramesToAverage;
        readonly List<byte[]> _previousFrames;

        public FadingAverageOperation(int numberOfFramesToAverage, string? darkFramePath, bool enableRegistration, ROI? registrationRegion, CancellationToken ct)
            : base(darkFramePath, enableRegistration ? RegistrationMethod.MirrorBorders : RegistrationMethod.None, registrationRegion, ct)
        {
            _numberOfFramesToAverage = numberOfFramesToAverage;
            _previousFrames = new List<byte[]>(_numberOfFramesToAverage);
        }

        public override void ProcessImage(BitmapSource image)
        {
            if (_previousFrames.Count >= _numberOfFramesToAverage)
                _previousFrames.RemoveAt(0);
            var pixels = PrepareFrame(image);
            _previousFrames.Add(pixels);
            Parallel.For(0, pixels.Length, i =>
            {
                uint acc = 0;
                for (int f = 0; f < _previousFrames.Count; f++)
                    acc += (uint)_previousFrames[f][i] * (uint)(f + 1);
                _accumulatorPixels![i] = acc;
            });
        }
        
        protected override double GetResultScaling()
        {
            return 0.5 / (_previousFrames.Count * (1 + _previousFrames.Count));
        }
    }
}
