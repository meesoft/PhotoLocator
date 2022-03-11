// Based on example from https://www.codeproject.com/Questions/815338/Inserting-GPS-tags-into-jpeg-EXIF-metadata-using-n

using MapControl;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    class ExifHandler
    {
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
            using var originalFileStream = File.Open(sourceFileName, FileMode.Open, FileAccess.Read);
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
            using var targetFileStream = File.Open(targetFileName, FileMode.Create, FileAccess.ReadWrite);
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
            using Stream savedFile = File.Open(sourceFileName, FileMode.Open, FileAccess.Read);
            var decoder = BitmapDecoder.Create(savedFile, CreateOptions, BitmapCacheOption.OnDemand);
            var metadata = decoder.Frames[0].Metadata as BitmapMetadata;
            return GetGeotag(metadata);
        }

        public static Location? GetGeotag(BitmapMetadata? metadata)
        {
            if (metadata is null)
                return null;

            var latitudeRef = (string)metadata.GetQuery(GpsLatitudeRefQuery);
            if (latitudeRef is null)
                return null;
            var latitude = metadata.GetQuery(GpsLatitudeQuery);
            GPSRational latitudeRational;
            if (latitude is byte[] latitudeBytes)
                latitudeRational = new GPSRational(latitudeBytes);
            else if (latitude is long[] latitude64)
                latitudeRational = new GPSRational(latitude64);
            else
                return null;
            if (latitudeRef == "S")
                latitudeRational.AngleInDegrees = -latitudeRational.AngleInDegrees;

            var longitudeRef = (string)metadata.GetQuery(GpsLongitudeRefQuery);
            if (longitudeRef is null)
                return null;
            GPSRational longitudeRational;
            var longitude = metadata.GetQuery(GpsLongitudeQuery);
            if (longitude is byte[] longitudeBytes)
                longitudeRational = new GPSRational(longitudeBytes);
            else if (longitude is long[] longitude64)
                longitudeRational = new GPSRational(longitude64);
            else
                return null;
            if (longitudeRef == "W")
                longitudeRational.AngleInDegrees = -longitudeRational.AngleInDegrees;

            //byte[] altitude = (byte[])metadata.GetQuery(GpsAltitudeQuery);

            return new Location(latitude: latitudeRational.AngleInDegrees, longitude: longitudeRational.AngleInDegrees);
        }
    }

    /// <summary>
    /// EXIF Rational Type (pack 4-byte numerator and 4-byte denominator into 8 bytes
    /// </summary>
    public class Rational
    {
        public readonly int Num;     //numerator of exif rational
        public readonly int Denom;   //denominator of exif rational
        public readonly long Bytes;   //8 bytes that form the exif rational value

        //form rational from a given 4-byte numerator and denominator
        public Rational(int num, int denom)
        {
            Num = num;
            Denom = denom;
            var bytes = new byte[8];  //create a byte array with 8 bytes
            BitConverter.GetBytes(Num).CopyTo(bytes, 0);  //copy 4 bytes of num to location 0 in the byte array
            BitConverter.GetBytes(Denom).CopyTo(bytes, 4);  //copy 4 bytes of denom to location 4 in the byte array
            Bytes = BitConverter.ToInt64(bytes);
        }

        //form rational from an Int64
        public Rational(long bytes) : this(BitConverter.GetBytes(bytes))
        {
        }

        //form rational from an array of 8 bytes
        public Rational(byte[] bytes)
        {
            Bytes = BitConverter.ToInt64(bytes);
            //convert the 4 bytes from n into a 4-byte int (becomes the numerator of the rational)
            Num = BitConverter.ToInt32(bytes, 0);
            //convert the 4 bytes from d into a 4-byte int (becomes the denonimator of the rational)
            Denom = BitConverter.ToInt32(bytes, 4);
        }

        //convert the exif rational into a double value
        public double ToDouble()
        {
            //round the double value to 5 digits
            return Math.Round(Convert.ToDouble(Num) / Convert.ToDouble(Denom), 5);
        }
    }

    /// <summary>
    /// Special rational class to handle the GPS three rational values  (degrees, minutes, seconds)
    /// </summary>
    public class GPSRational
    {
        public readonly Rational Degrees;
        public readonly Rational Minutes;
        public readonly Rational Seconds;
        public readonly long[] Bytes;  //becomes an array of 3 longs that represent hrs, minutes, seconds as 3 rationals
        public double AngleInDegrees;  //latitude or longitude as decimal degrees

        //form the 3-rational exif value from an angle in decimal degrees
        public GPSRational(double angleInDeg)
        {
            //convert angle in decimal degrees to three rationals (deg, min, sec) with denominator of 1
            //NOTE:  this formulation results in a descretization of about 100 ft in the lat/lon position
            var absAngleInDeg = Math.Abs(angleInDeg);
            var degreesInt = (int)absAngleInDeg;
            absAngleInDeg -= degreesInt;
            var minutesInt = (int)(absAngleInDeg * 60.0);
            absAngleInDeg -= minutesInt / 60.0;
            var secondsInt = (int)(absAngleInDeg * 3600.0 + 0.50);

            //form a rational using "1" as the denominator
            var denominator = 1;
            Degrees = new Rational(degreesInt, denominator);
            Minutes = new Rational(minutesInt, denominator);
            Seconds = new Rational(secondsInt, denominator);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = new long[3] { Degrees.Bytes, Minutes.Bytes, Seconds.Bytes };
        }

        //Form the GPSRational object from an array of 24 bytes
        public GPSRational(byte[] bytes)
        {
            var degBytes = new byte[8]; var minBytes = new byte[8]; var secBytes = new byte[8];

            //form the hours, minutes, seconds rational values from the input 24 bytes
            // first 8 are hours, second 8 are the minutes, third 8 are the seconds
            Array.Copy(bytes, 0, degBytes, 0, 8); Array.Copy(bytes, 8, minBytes, 0, 8); Array.Copy(bytes, 16, secBytes, 0, 8);

            Degrees = new Rational(degBytes);
            Minutes = new Rational(minBytes);
            Seconds = new Rational(secBytes);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = new long[3] { Degrees.Bytes, Minutes.Bytes, Seconds.Bytes };
        }

        public GPSRational(long[] bytes)
        {
            Degrees = new Rational(bytes[0]);
            Minutes = new Rational(bytes[1]);
            Seconds = new Rational(bytes[2]);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = bytes;
        }
    }
}