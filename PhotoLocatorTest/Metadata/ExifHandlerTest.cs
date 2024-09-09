using PhotoLocator.PictureFileFormats;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    [TestClass]
    public class ExifHandlerTest
    {
        static readonly string _jpegTestDataTimestamp = new DateTime(2022, 6, 17, 19, 3, 2).ToString();

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnJpeg()
        {
            const string TestFileName = "fromJpeg.jpg";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source), ExifHandler.GetMetadataString(target));
            Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnTiff()
        {
            const string TestFileName = "fromJpeg.tif";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source), ExifHandler.GetMetadataString(target));
            //Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnPng()
        {
            const string TestFileName = "fromJpeg.png";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target));
            Assert.AreEqual(1e-4, ExifHandler.GetGeotag(source)!.Latitude, ExifHandler.GetGeotag(target)!.Latitude);
        }

        [TestMethod]
        public void SetMetadata_ShouldSetPngMetadataOnJpeg()
        {
            const string TestFileName = "fromPng.jpg";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            source = ExifHandler.CreateMetadataForEncoder(source, new PngBitmapEncoder());

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target));
            Assert.AreEqual(1e-4, ExifHandler.GetGeotag(source)!.Longitude, ExifHandler.GetGeotag(target)!.Longitude);
        }

        [TestMethod]
        public void SetMetadata_ShouldSetPngMetadataOnTiff()
        {
            const string TestFileName = "fromPng.tif";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            source = ExifHandler.CreateMetadataForEncoder(source, new PngBitmapEncoder());

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target));
            //Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetTiffMetadataOnJpeg()
        {
            const string TestFileName = "fromTiff.jpg";

            using var stream = File.OpenRead(@"TestData\RGB48.tif");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var target = ExifHandler.LoadMetadata(File.OpenRead(TestFileName))!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source), ExifHandler.GetMetadataString(target));
            Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

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
            ExifHandler.SetGeotag(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out1.jpg", setValue, null);

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

        [TestMethod]
        public void GetMetadataString_ShouldFormatMetadata()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var metadata = ExifHandler.LoadMetadata(stream);
            Assert.IsNotNull(metadata);
            var str = ExifHandler.GetMetadataString(metadata);
            Assert.AreEqual("FC7303, 100.7m, 1/80s, f/2.8, 4.49mm, ISO100, 06/17/2022 19:03:02", str);
        }

        [TestMethod, Ignore]
        public void GetMetadataString_ShouldFormatMetadata_Performance()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                using var stream = File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg");
                var metadata = ExifHandler.LoadMetadata(stream);
                Assert.IsNotNull(metadata);
                var str = ExifHandler.GetMetadataString(metadata);
                Assert.AreEqual("FC7303, 100.7m, 1/80s, f/2.8, 4.49mm, ISO100, 06/17/2022 19:03:02", str);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
