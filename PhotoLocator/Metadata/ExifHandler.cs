// Based on example from https://www.codeproject.com/Questions/815338/Inserting-GPS-tags-into-jpeg-EXIF-metadata-using-n

using MapControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    class ExifHandler
    {
        // See https://exiv2.org/tags.html
        private const string ExposureTimeQuery = "/app1/ifd/exif/subifd:{uint=33434}"; // RATIONAL 1
        private const string LensApertureQuery = "/app1/ifd/exif/subifd:{uint=33437}"; // RATIONAL 1
        private const string IsoQuery = "/app1/ifd/exif/subifd:{uint=34855}"; // Short
        // North or South Latitude 
        private const string GpsLatitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=1}"; // ASCII 2
        // Latitude        
        private const string GpsLatitudeQuery = "/app1/ifd/gps/subifd:{ulong=2}"; // RATIONAL 3
        // East or West Longitude 
        private const string GpsLongitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=3}"; // ASCII 2
        // Longitude 
        private const string GpsLongitudeQuery = "/app1/ifd/gps/subifd:{ulong=4}"; // RATIONAL 3
        // Altitude reference 
        private const string GpsAltitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=5}"; // BYTE 1
        // Altitude 
        private const string GpsAltitudeQuery = "/app1/ifd/gps/subifd:{ulong=6}"; // RATIONAL 1

        const BitmapCreateOptions CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;

        public static void SetGeotag(string sourceFileName, string targetFileName, Location location)
        {
            MemoryStream memoryStream;
            using (var originalFileStream = File.Open(sourceFileName, FileMode.Open, FileAccess.Read))
            {
                // Decode
                var sourceSize = originalFileStream.Length;
                var decoder = BitmapDecoder.Create(originalFileStream, CreateOptions, BitmapCacheOption.None);
                var frame = decoder.Frames[0];

                // Tag
                var metadata = frame.Metadata is null ? new BitmapMetadata("jpg") : (BitmapMetadata)frame.Metadata.Clone();
                SetGeotag(metadata, location);

                // Encode
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame, frame.Thumbnail, metadata, frame.ColorContexts));
                memoryStream = new MemoryStream();
                encoder.Save(memoryStream);

                // Check
                memoryStream.Position = 0;
                CheckPixels(frame, BitmapDecoder.Create(memoryStream, CreateOptions, BitmapCacheOption.None).Frames[0]);
            }
            // Save
            using var targetFileStream = File.Open(targetFileName, FileMode.Create, FileAccess.Write);
            memoryStream.Position = 0;
            memoryStream.CopyTo(targetFileStream);
            memoryStream.Dispose();
        }

        private static void CheckPixels(BitmapFrame frame1, BitmapFrame frame2)
        {
            if (frame1.PixelWidth != frame2.PixelWidth || frame1.PixelHeight != frame2.PixelHeight)
                throw new Exception("Dimensions have changed");

            var bytesPerLine = frame1.PixelWidth * 3;
            var pixels1 = new byte[bytesPerLine * frame1.PixelHeight];
            frame1.CopyPixels(pixels1, bytesPerLine, 0);

            var pixels2 = new byte[bytesPerLine * frame2.PixelHeight];
            frame2.CopyPixels(pixels2, bytesPerLine, 0);
            for (var i = 0; i < pixels1.Length; i++)
                if (pixels1[i] != pixels2[i])
                    throw new Exception("Pixels have changed");
        }

        public static void SetGeotag(BitmapMetadata metadata, Location location)
        {
            const uint PaddingAmount = 2048;

            //pad the metadata so that it can be expanded with new tags
            metadata.SetQuery("/app1/ifd/PaddingSchema:Padding", PaddingAmount);
            metadata.SetQuery("/app1/ifd/exif/PaddingSchema:Padding", PaddingAmount);
            metadata.SetQuery("/xmp/PaddingSchema:Padding", PaddingAmount);

            var latitudeRational = new GPSRational(location.Latitude);
            var longitudeRational = new GPSRational(location.Longitude);
            metadata.SetQuery(GpsLatitudeQuery, latitudeRational.Bytes);
            metadata.SetQuery(GpsLongitudeQuery, longitudeRational.Bytes);
            if (location.Latitude >= 0)
                metadata.SetQuery(GpsLatitudeRefQuery, "N");
            else
                metadata.SetQuery(GpsLatitudeRefQuery, "S");
            if (location.Longitude >= 0)
                metadata.SetQuery(GpsLongitudeRefQuery, "E");
            else
                metadata.SetQuery(GpsLongitudeRefQuery, "W");

            //Rational altitudeRational = new Rational((int)altitude, 1);  //denoninator = 1 for Rational
            //metadata.SetQuery(GpsAltitudeQuery, altitudeRational.bytes);
        }

        public static Location? GetGeotag(string sourceFileName)
        {
            using var file = File.OpenRead(sourceFileName);
            var decoder = BitmapDecoder.Create(file, CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = decoder.Frames[0].Metadata as BitmapMetadata;
            return GetGeotag(metadata);
        }

        public static Location? GetGeotag(BitmapMetadata? metadata)
        {
            if (metadata is null)
                return null;

            if (metadata.GetQuery(GpsLatitudeRefQuery) is not string latitudeRef)
                return null;
            var latitude = GPSRational.Decode(metadata.GetQuery(GpsLatitudeQuery));
            if (latitude is null)
                return null;
            if (latitudeRef == "S")
                latitude.AngleInDegrees = -latitude.AngleInDegrees;

            if (metadata.GetQuery(GpsLongitudeRefQuery) is not string longitudeRef)
                return null;
            var longitude = GPSRational.Decode(metadata.GetQuery(GpsLongitudeQuery));
            if (longitude is null)
                return null;
            if (longitudeRef == "W")
                longitude.AngleInDegrees = -longitude.AngleInDegrees;

            //byte[] altitude = (byte[])metadata.GetQuery(GpsAltitudeQuery);

            return new Location(latitude: latitude.AngleInDegrees, longitude: longitude.AngleInDegrees);
        }

        public static string GetMetataString(string fileName)
        {
            using var file = File.OpenRead(fileName);
            var decoder = BitmapDecoder.Create(file, CreateOptions, BitmapCacheOption.OnDemand);
            if (decoder.Frames[0].Metadata is not BitmapMetadata metadata)
                return string.Empty;
            var metadataStrings = new List<string>();

            if (!string.IsNullOrEmpty(metadata.CameraModel))
                metadataStrings.Add(metadata.CameraModel);

            var exposureTime = Rational.Decode(metadata.GetQuery(ExposureTimeQuery));
            if (exposureTime != null)
                metadataStrings.Add(exposureTime.ToDouble() + "s");

            var lensAperture = Rational.Decode(metadata.GetQuery(LensApertureQuery));
            if (lensAperture != null)
                metadataStrings.Add("f/" + lensAperture.ToDouble());

            var iso = metadata.GetQuery(IsoQuery);
            if (iso != null)
                metadataStrings.Add("ISO" + iso.ToString());

            if (!string.IsNullOrEmpty(metadata.DateTaken))
                metadataStrings.Add(metadata.DateTaken);

            return string.Join(", ", metadataStrings);
        }
    }
}