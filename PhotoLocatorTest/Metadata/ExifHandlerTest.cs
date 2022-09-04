﻿using System.Windows.Media.Imaging;

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
    }
}