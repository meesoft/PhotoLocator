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
        const string ExifToolPath = @"exiftool\exiftool(-m).exe";

        static readonly DateTimeOffset _jpegTestDataTimestamp = LocalTimeToDateTimeOffset(new DateTime(2022, 6, 17, 19, 3, 2));
        
        static DateTimeOffset LocalTimeToDateTimeOffset(DateTime dateTime) => new(dateTime, TimeZoneInfo.Local.GetUtcOffset(dateTime));

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnJpeg()
        {
            const string TestFileName = "fromJpeg.jpg";

            using var sourceStream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(sourceStream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source, sourceStream), ExifHandler.GetMetadataString(target, targetStream));
            Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnTiff()
        {
            const string TestFileName = "fromJpeg.tif";

            using var sourceStream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(sourceStream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source, sourceStream), ExifHandler.GetMetadataString(target, targetStream));
            //Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetJpegMetadataOnJpegXr()
        {
            const string TestFileName = "fromJpeg.jxr";

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var source = ExifHandler.LoadMetadata(stream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual("FC7303, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual("1/80s, " + _jpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
            //Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void SetMetadata_ShouldSetTiffMetadataOnJpeg()
        {
            const string TestFileName = "fromTiff.jpg";

            using var sourceStream = File.OpenRead(@"TestData\RGB48.tif");
            var source = ExifHandler.LoadMetadata(sourceStream) ?? throw new Exception("Unable to load metadata");
            Assert.IsFalse(string.IsNullOrEmpty(source.CameraModel));

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);

            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, source);

            using var targetStream = File.OpenRead(TestFileName);
            var target = ExifHandler.LoadMetadata(targetStream)!;
            Assert.AreEqual(source.CameraModel, target.CameraModel);
            Assert.AreEqual(ExifHandler.GetMetadataString(source, sourceStream), ExifHandler.GetMetadataString(target, targetStream));
            Assert.AreEqual(ExifHandler.GetGeotag(source), ExifHandler.GetGeotag(target));
        }

        [TestMethod]
        public void DecodeTimeStamp_ShouldDecodeTimeStamp_InLocalTime()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var timeStamp = ExifHandler.DecodeTimeStamp(metadata, stream) ?? throw new FileFormatException("Failed to decode timestamp");

            Assert.AreEqual(_jpegTestDataTimestamp, timeStamp);
        }

        [TestMethod]
        public void DecodeTimeStamp_ShouldDecodeTimeStamp_WithOffset()
        {
            using var stream = File.OpenRead(@"TestData\2025-05-04_15.13.08-04.jpg");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var timeStamp = ExifHandler.DecodeTimeStamp(metadata, stream) ?? throw new FileFormatException("Failed to decode timestamp");

            Assert.AreEqual(new(2025, 5, 4, 15, 13, 8, TimeSpan.FromHours(-4)), timeStamp);
        }

        [TestMethod]
        public void DecodeTimeStamp_ShouldDecodeTimeStamp_WithOffsetInCanonMakerNotes()
        {
            using var stream = File.OpenRead(@"TestData\CanonR10-2025-05-11 14.26.42+9.jpg");
            var decoder = BitmapDecoder.Create(stream, ExifHandler.CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = (BitmapMetadata)decoder.Frames[0].Metadata;

            var timeStamp = ExifHandler.DecodeTimeStamp(metadata, stream) ?? throw new FileFormatException("Failed to decode timestamp");

            Assert.AreEqual(new(2025, 5, 11, 14, 26, 42, TimeSpan.FromHours(9)), timeStamp);
        }

        [TestMethod]
        public async Task AdjustTimestampAsync_ShouldUpdateTimestamp()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            const string TargetFileName = @"TestData\2022-06-17_18.03.02.jpg";

            await ExifHandler.AdjustTimeStampAsync(@"TestData\2022-06-17_19.03.02.jpg", TargetFileName, "-01:00:00", ExifToolPath, default);

            using var targetFile = File.OpenRead(TargetFileName);
            var metadata = ExifHandler.LoadMetadata(targetFile);
            var tag = ExifHandler.DecodeTimeStamp(metadata!, targetFile) ?? throw new FileFormatException("Failed to decode timestamp");
            Assert.AreEqual(new DateTime(2022, 06, 17, 18, 03, 02, DateTimeKind.Local), tag);
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
        public async Task SetGeotag_ShouldSet_UsingBitmapMetadata()
        {
            var setValue = new MapControl.Location(-10, -20);
            await ExifHandler.SetGeotagAsync(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out1.jpg", setValue, null, default);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out1.jpg");
            Assert.AreEqual(setValue, newValue);
        }
      
        [TestMethod]
        public async Task SetGeotag_ShouldSet_UsingExifTool()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            await ExifHandler.SetGeotagAsync(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out2.jpg", setValue, ExifToolPath, default);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out2.jpg");
            Assert.AreEqual(setValue, newValue);
        }

        [TestMethod]
        public async Task SetGeotag_ShouldSet_UsingExifTool_InPlace()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            File.Copy(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02_copy.jpg", true);
            await ExifHandler.SetGeotagAsync(@"TestData\2022-06-17_19.03.02_copy.jpg", @"TestData\2022-06-17_19.03.02_copy.jpg", setValue, ExifToolPath, default);

            var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02_copy.jpg");
            Assert.AreEqual(setValue, newValue);
        }

        [TestMethod]
        public async Task SetGeotag_ShouldSetInCr3_UsingExifTool()
        {
            const string FileName = @"TestData\Test.CR3";
            if (!File.Exists(FileName))
                Assert.Inconclusive("Image not found");
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            var setValue = new MapControl.Location(-10, -20);
            await ExifHandler.SetGeotagAsync(FileName, "tagged.cr3", setValue, ExifToolPath, default);

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
            var str = ExifHandler.GetMetadataString(metadata, stream);
            Assert.AreEqual("FC7303, 100.7m, 1/80s, f/2.8, 4.49mm, ISO100, " + _jpegTestDataTimestamp, str);
        }

        [TestMethod]
        public void DecodeMetadata_ShouldDecodeUsingExifTool()
        {
            if (!File.Exists(ExifToolPath))
                Assert.Inconclusive("ExifTool not found");

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var metadata = ExifHandler.DecodeMetadataUsingExifTool(@"TestData\2022-06-17_19.03.02.jpg", ExifToolPath);

            Assert.AreEqual("FC7303, 1/80s, f/2.8, 4.5 mm, ISO100, 341x191, " + _jpegTestDataTimestamp, metadata.Metadata);
            Assert.AreEqual(new DateTimeOffset(2022, 6, 17, 19, 3, 2, TimeSpan.FromHours(2)), metadata.TimeStamp);
            Assert.AreEqual(55.4, metadata.Location!.Latitude, 0.1);
            Assert.AreEqual(11.2, metadata.Location!.Longitude, 0.1);
        }

        [TestMethod]
        public void DecodeTimestampFromExifTool_ShouldUseOffset()
        {
            var dict = new Dictionary<string, string>
            {
                { "DateTimeOriginal", "2025:02:01 22:14:54" },
                { "OffsetTimeOriginal", "+01:00" }
            };

            var timestamp = ExifHandler.DecodeTimeStampFromExifTool(dict);

            Assert.AreEqual(new DateTimeOffset(2025, 2, 1, 22, 14, 54, TimeSpan.FromHours(1)), timestamp);
        }

        [TestMethod]
        [DataRow(@"TestData\Canon90DVideo.txt", 2024, 7, 9, 14, 38, 53, 0, +2)]
        [DataRow(@"TestData\DJIAction2Video.txt", 2022, 4, 16, 18, 46, 28, 0, +2)]
        [DataRow(@"TestData\iPhoneVideo.txt", 2022, 9, 23, 12, 50, 53, 0, +2)]
        [DataRow(@"TestData\Mini2Video.txt", 2024, 7, 9, 13, 9, 22, 0, +2)]
        [DataRow(@"TestData\Pixel5Video.txt", 2025, 4, 26, 17, 6, 45, 0, +2)]
        public void DecodeTimestampFromExifTool_ShouldHandleDifferentFormats(string fileName, int year, int month, int day, int hour, int minutes, int seconds, int ms, int offset)
        {
            var metadata = ExifHandler.DecodeExifToolMetadataToDictionary(File.ReadAllLines(fileName));

            var decoded = ExifHandler.DecodeTimeStampFromExifTool(metadata);

            Assert.AreEqual(new DateTimeOffset(year, month, day, hour, minutes, seconds, ms, TimeSpan.FromHours(offset)), decoded);
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
                var str = ExifHandler.GetMetadataString(metadata, stream);
                Assert.AreEqual("FC7303, 100.7m, 1/80s, f/2.8, 4.49mm, ISO100, 06/17/2022 19:03:02", str);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
