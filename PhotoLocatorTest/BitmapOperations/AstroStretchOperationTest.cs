using PhotoLocator.PictureFileFormats;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class AstroStretchOperationTest
    {
        [TestMethod]
        public void Apply_AstroStretchOperation()
        {
            var source = BitmapDecoder.Create(File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

            var sw = Stopwatch.StartNew();  
            var sourceFloat = new FloatBitmap(source, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var op = new AstroStretchOperation()
            {
                SrcBitmap = sourceFloat,
                DstBitmap = new FloatBitmap(),
                Stretch = AstroStretchOperation.OptimizeStretch(sourceFloat),
            };
            op.Apply();
            Console.WriteLine(sw.ElapsedMilliseconds);

            sw.Restart();
            var result = op.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
            Console.WriteLine(sw.ElapsedMilliseconds);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, "astroStretch.jpg");
#endif
            Assert.AreEqual(0.04771542762189313, op.DstBitmap.Mean(), 1e-5);
        }
    }
}
