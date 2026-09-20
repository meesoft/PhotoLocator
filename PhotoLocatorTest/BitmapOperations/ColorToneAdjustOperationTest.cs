using PhotoLocator.PictureFileFormats;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class ColorToneAdjustOperationTest
    {
        [TestMethod]
        public void Apply_ColorToneAdjustOperation()
        {
            var source = BitmapDecoder.Create(File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

            var sw = Stopwatch.StartNew();  
            var sourceFloat = new FloatBitmap(source, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var op = new ColorToneAdjustOperation()
            {
                SrcBitmap = sourceFloat,
                DstBitmap = new FloatBitmap(),
            };
            op.ToneAdjustments[2].AdjustSaturation = 0.5f;
            op.Apply();
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var result = op.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, "ColorToneAdjust.png");
#endif
        }

        [TestMethod]
        public void ApplySingleToneAdjustments_OppositeHueInterpolationReducesSaturation()
        {
            var source = new FloatBitmap(1, 1, 3);
            ColorToneAdjustOperation.ColorTransformHSI2RGB(1f / 16, 1, 0.4f,
                out source.Elements[0, 0], out source.Elements[0, 1], out source.Elements[0, 2]);

            var operation = new ColorToneAdjustOperation
            {
                SrcBitmap = source,
                DstBitmap = new FloatBitmap(),
            };
            operation.ToneAdjustments[0].AdjustHue = 0;
            operation.ToneAdjustments[1].AdjustHue = 0.5f;

            operation.Apply();

            ColorToneAdjustOperation.ColorTransformRGB2HSI(
                operation.DstBitmap.Elements[0, 0], operation.DstBitmap.Elements[0, 1], operation.DstBitmap.Elements[0, 2],
                out _, out var saturation, out _);
            Assert.IsTrue(saturation < 0.001f, $"Saturation was {saturation}");
        }
    }
}
