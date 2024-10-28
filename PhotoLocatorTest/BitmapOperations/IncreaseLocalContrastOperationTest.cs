using PhotoLocator.PictureFileFormats;
using System.Diagnostics;
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

            var sw = Stopwatch.StartNew();  
            var sourceFloat = new FloatBitmap(source, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var op = new IncreaseLocalContrastOperation()
            {
                SrcBitmap = sourceFloat,
                DstBitmap = new FloatBitmap(),
                OutlierReductionFilterSize = 2,
            };
            op.Apply();
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var result = op.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, "localContrast.png");
#endif
        }
    }
}
