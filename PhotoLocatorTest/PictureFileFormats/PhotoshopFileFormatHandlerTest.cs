using MapControl;
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
            PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, CancellationToken.None);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadG16()
        {
            using var stream = File.OpenRead(@"TestData\G16.psd");
            PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, CancellationToken.None);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadRGB8()
        {
            using var stream = File.OpenRead(@"TestData\RGB8.psd");
            PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, CancellationToken.None);
        }

        [TestMethod]
        public void LoadFromStream_ShouldLoadRGB16()
        {
            using var stream = File.OpenRead(@"TestData\RGB16.psd");
            PhotoshopFileFormatHandler.LoadFromStream(stream, Rotation.Rotate0, 100, CancellationToken.None);
        }
    }
}
