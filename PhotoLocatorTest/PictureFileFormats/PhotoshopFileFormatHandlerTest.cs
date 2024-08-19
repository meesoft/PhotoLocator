using MapControl;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Metadata;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    [TestClass]
    public class PhotoshopFileFormatHandlerTest
    {
        [TestMethod]
        public void LoadFromStream_ShouldLoadG8()
        {
            using var stream = File.OpenRead(@"TestData\G8.psd");
            var image = PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, false, default);
            new FloatBitmap(image, 1);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadG16()
        {
            using var stream = File.OpenRead(@"TestData\G16.psd");
            var image = PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, false, default);
            new FloatBitmap(image, 1);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadRGB8()
        {
            using var stream = File.OpenRead(@"TestData\RGB8.psd");
            var image = PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, false, default);
            new FloatBitmap(image, 1);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadRGB16()
        {
            using var stream = File.OpenRead(@"TestData\RGB16.psd");
            var image = PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, false, default);
            new FloatBitmap(image, 1);
        }
    }
}
