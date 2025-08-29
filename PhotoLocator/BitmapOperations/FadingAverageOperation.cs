using System;
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

        public FadingAverageOperation(int numberOfFramesToAverage, string? darkFramePath, CombineFramesRegistration? registrationSettings, CancellationToken ct)
            : base(darkFramePath, registrationSettings?.ToCombineFramesRegistrationFull(RegistrationOperation.Borders.Mirror), ct)
        {
            _numberOfFramesToAverage = numberOfFramesToAverage;
            _previousFrames = new List<byte[]>(_numberOfFramesToAverage);
        }

        public override void ProcessImage(BitmapSource image)
        {
            var pixels = PrepareFrame(image);
            if (_previousFrames.Count >= _numberOfFramesToAverage)
                _previousFrames.RemoveAt(0);
            _previousFrames.Add(pixels);

            Array.Clear(_accumulatorPixels!);
            int stride = Width * PixelSize;
            for (int f = 0; f < _previousFrames.Count; f++)
            {
                var frame = _previousFrames[f];
                var scale = (uint)(f + 1);
                Parallel.For(0, Height, y =>
                {
                    unsafe
                    {
                        fixed (byte* frameRow = &frame[y * stride])
                        fixed (uint* accumulatorRow = &_accumulatorPixels![y * stride])
                            for (int x = 0; x < stride; x++)
                                accumulatorRow[x] += frameRow[x] * scale;
                    }
                });
            }
        }

        protected override double GetResultScaling()
        {
            return 2.0 / (_previousFrames.Count * (1 + _previousFrames.Count));
        }
    }
}
