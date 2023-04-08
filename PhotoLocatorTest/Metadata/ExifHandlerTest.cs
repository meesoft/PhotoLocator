using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    [TestClass]
    public class ExifHandlerTest
    {
        [TestMethod]
        public void GetTimeStamp_ShouldDecodeTimeStamp()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var tag = ExifHandler.GetTimeStamp(metadata) ?? throw new FileFormatException("Failed to decode timestamp");

            Assert.AreEqual(new DateTime(2022, 06, 17, 19, 03, 02, DateTimeKind.Local), tag);
        }

        [TestMethod]
        public void GetGeotag_ShouldDecodeGpsCoords()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var tag = ExifHandler.GetGeotag(metadata) ?? throw new FileFormatException("Failed to decode geotag");

            Assert.AreEqual(55.4, tag.Latitude, 0.1);
            Assert.AreEqual(11.2, tag.Longitude, 0.1);
        }

        [TestMethod]
        public void GetRelativeAltitude_ShouldDecodeAltitude()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var tag = ExifHandler.GetRelativeAltitude(metadata) ?? throw new FileFormatException("Failed to decode altitude");

            Assert.AreEqual(100.7, tag, 0.1);
        }

        [TestMethod]
        public void GetGpsAltitude_ShouldDecodeAltitude()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var tag = ExifHandler.GetGpsAltitude(metadata) ?? throw new FileFormatException("Failed to decode altitude");

            Assert.AreEqual(35.0, tag, 0.1);
        }

        [TestMethod]
        public void SetGeotag_ShouldSet_UsingBitmapMetadata()
        {
            var setValue = new MapControl.Location(-10, -20);
            ExifHandler.SetGeotag(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out1.jpg", setValue);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out1.jpg");
            Assert.AreEqual(setValue, newValue);
        }

        const string ExifToolPath = @"TestData\exiftool.exe";
      
        [TestMethod]
        public void SetGeotag_ShouldSet_UsingExifTool()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            ExifHandler.SetGeotag(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out2.jpg", setValue, ExifToolPath);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out2.jpg");
            Assert.AreEqual(setValue, newValue);
        }

        [TestMethod]
        public void SetGeotag_ShouldSet_UsingExifTool_InPlace()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            ExifHandler.SetGeotag(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02.jpg", setValue, ExifToolPath);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02.jpg");
            Assert.AreEqual(setValue, newValue);
        }

        [TestMethod]
        public void SetGeotag_ShouldSetInCr3_UsingExifTool()
        {
            const string FileName = @"TestData\Test.CR3";
            if (!File.Exists(FileName))
                Assert.Inconclusive("Image not found");
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            ExifHandler.SetGeotag(FileName, "tagged.cr3", setValue, ExifToolPath);

            var newValue = ExifHandler.GetGeotag("tagged.cr3");
            Assert.AreEqual(setValue, newValue);
        }
    }
}
