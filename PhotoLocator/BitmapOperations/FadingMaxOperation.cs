using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    sealed class FadingMaxOperation : CombineFramesOperationBase
    {
        readonly int _numberOfFramesToAverage;
        readonly List<byte[]> _previousFrames;

        public FadingMaxOperation(int numberOfFramesToAverage, string? darkFramePath, bool enableRegistration, ROI? registrationRegion, CancellationToken ct)
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
            int max = _previousFrames.Count * 255;
            Parallel.For(0, pixels.Length, i =>
            {
                int acc = 0;
                for (int f = 0; f < _previousFrames.Count; f++)
                    acc = Math.Clamp(_previousFrames[f][i] * (f + 1), acc, max);
                _accumulatorPixels![i] = (uint)acc;
            });
        }
        
        protected override double GetResultScaling()
        {
            return 1.0 / _previousFrames.Count;
        }
    }
}
