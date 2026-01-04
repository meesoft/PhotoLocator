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
        public static readonly DateTimeOffset JpegTestDataTimestamp = LocalTimeToDateTimeOffset(new DateTime(2022, 6, 17, 19, 3, 2));
        
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

            using var targetStream = File.OpenRead(TestFileName);
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
            Assert.AreEqual("FC7303, " + JpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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
            Assert.AreEqual("1/80s, " + JpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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
            Assert.AreEqual("1/80s, " + JpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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
            Assert.AreEqual("1/80s, " + JpegTestDataTimestamp, ExifHandler.GetMetadataString(target, targetStream));
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

            Assert.AreEqual(JpegTestDataTimestamp, timeStamp);
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
        public void GetMetadataString_ShouldFormatMetadata()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var metadata = ExifHandler.LoadMetadata(stream);
            Assert.IsNotNull(metadata);
            var str = ExifHandler.GetMetadataString(metadata, stream);
            Assert.AreEqual("FC7303, 100.7m, 1/80s, f/2.8, 4.49mm, ISO100, " + JpegTestDataTimestamp, str);
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

        [TestMethod]
        public void ResetOrientation_ShouldBeApplied_WhenSavingProcessedImage()
        {
            const string TestFileName = "orientation_reset_test.jpg";

            using var sourceStream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var metadata = ExifHandler.LoadMetadata(sourceStream) ?? throw new Exception("Unable to load metadata");

            // Set orientation to simulate rotated image
            metadata = metadata.Clone();
            metadata.SetQuery(ExifHandler.OrientationQuery1, (ushort)6);

            // Create a test bitmap
            var bitmap = BitmapSource.Create(10, 10, 96, 96, PixelFormats.Bgr24, null, new byte[10 * 10 * 3], 10 * 3);

            // Apply ResetOrientation and save
            var resetMetadata = ExifHandler.ResetOrientation(metadata);
            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, resetMetadata, 90);

            // Verify orientation was reset
            using var targetStream = File.OpenRead(TestFileName);
            var savedMetadata = ExifHandler.LoadMetadata(targetStream)!;
            var orientationValue = savedMetadata.GetQuery(ExifHandler.OrientationQuery1) as ushort?;

            Assert.AreEqual((ushort)1, orientationValue);
        }

        [TestMethod]
        public void ResetOrientation_ShouldPreserveOtherMetadata()
        {
            const string TestFileName = "metadata_preservation_test.jpg";

            using var sourceStream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-06-17_19.03.02.jpg")
                ?? throw new FileNotFoundException("Resource not found");
            var metadata = ExifHandler.LoadMetadata(sourceStream) ?? throw new Exception("Unable to load metadata");

            var originalCameraModel = metadata.CameraModel;
            var originalGeotag = ExifHandler.GetGeotag(metadata);
            Assert.IsNotNull(originalGeotag);

            // Set orientation and reset it
            metadata = metadata.Clone();
            metadata.SetQuery(ExifHandler.OrientationQuery1, (ushort)3);
            var resetMetadata = ExifHandler.ResetOrientation(metadata);

            // Create and save bitmap
            var bitmap = BitmapSource.Create(10, 10, 96, 96, PixelFormats.Bgr24, null, new byte[10 * 10 * 3], 10 * 3);
            GeneralFileFormatHandler.SaveToFile(bitmap, TestFileName, resetMetadata, 90);

            // Verify other metadata is preserved
            using var targetStream = File.OpenRead(TestFileName);
            var savedMetadata = ExifHandler.LoadMetadata(targetStream)!;

            Assert.AreEqual(originalCameraModel, savedMetadata.CameraModel);
            var savedGeotag = ExifHandler.GetGeotag(savedMetadata);
            Assert.IsNotNull(savedGeotag);
            Assert.AreEqual(originalGeotag.Latitude, savedGeotag.Latitude, 0.001);
            Assert.AreEqual(originalGeotag.Longitude, savedGeotag.Longitude, 0.001);
        }
    }
}
