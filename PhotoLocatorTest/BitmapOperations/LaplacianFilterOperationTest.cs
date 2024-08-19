using MeeSoft.ImageProcessing.Operations;
using PhotoLocator.PictureFileFormats;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class LaplacianFilterOperationTest
    {
        [TestMethod]
        public void Apply_LaplacianFilter()
        {
            var source = BitmapDecoder.Create(File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg"), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var sourceFloat = new FloatBitmap(source, FloatBitmap.DefaultMonitorGamma);

            var op = new LaplacianFilterOperation()
            {
                SrcBitmap = sourceFloat,
                DstBitmap = new FloatBitmap(),
                Alpha = 0.5f,
                Beta = 0.5f,
                OutlierReductionFilterSize = 1,
            };
            op.Apply();

            var result = op.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, "laplacianPyramid.png");
#endif
        }
    }
}
