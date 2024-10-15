using PhotoLocator.PictureFileFormats;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class IncreaseLocalContrastOperationTest
    {
        [TestMethod]
        public void Apply_IncreaseLocalContrast()
        {
            var source = BitmapDecoder.Create(File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var sourceFloat = new FloatBitmap(source, FloatBitmap.DefaultMonitorGamma);

            var op = new IncreaseLocalContrastOperation()
            {
                SrcBitmap = sourceFloat,
                DstBitmap = new FloatBitmap(),
                OutlierReductionFilterSize = 2,
            };
            op.Apply();

            var result = op.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, "localContrast.png");
#endif
        }

        [TestMethod]
        public void FloatToByteGammaLutRange_ShouldBeSufficient()
        {
            const int TestRange = 256;
            double gamma = 2.2;

            var lut = FloatBitmap.CreateGammaLookupFloatToByte(gamma);
            var scale = 1.0 / (TestRange - 1);
            int diff = 0;
            for (int i = 0; i < TestRange; i++)
            {
                //float e = i / (float)TestRange;
                float e = (float)Math.Pow(i * scale, gamma);
                diff += Math.Abs(
                    (byte)(Math.Pow(e, 1 / gamma) * 255 + 0.5) -
                    lut[(int)(e * FloatBitmap.FloatToByteGammaLutRange + 0.5f)]);
            }
            Assert.AreEqual(0, diff);
        }
    }
}
