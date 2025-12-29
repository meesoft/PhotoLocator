using System;
using System.Threading;

namespace PhotoLocator.BitmapOperations
{
    sealed class TimeCompressionAverageOperation : CombineFramesOperationBase
    {
        readonly int _numberOfFramesToAverage;
        int _collectedFrames;

        public TimeCompressionAverageOperation(int numberOfFramesToAverage, string? darkFramePath, CombineFramesRegistration? registrationSettings, CancellationToken ct) 
            : base(darkFramePath, registrationSettings?.ToCombineFramesRegistrationBase(RegistrationOperation.Borders.Mirror), ct)
        {
            _numberOfFramesToAverage = numberOfFramesToAverage;
        }

        public override bool IsResultReady => _collectedFrames == _numberOfFramesToAverage;

        public override void ProcessImage(System.Windows.Media.Imaging.BitmapSource image)
        {
            if (_collectedFrames >= _numberOfFramesToAverage)
            {
                _collectedFrames = 0;
                Array.Clear(_accumulatorPixels!);
            }
            var pixels = PrepareFrame(image);
            for (int i = 0; i < pixels.Length; i++)
                _accumulatorPixels![i] += pixels[i];
            _collectedFrames++;
        }

        protected override double GetResultScaling()
        {
            return 1.0 / _numberOfFramesToAverage;
        }
    }
}
