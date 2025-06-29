// JPEG geotagging based on example from https://www.codeproject.com/Questions/815338/Inserting-GPS-tags-into-jpeg-EXIF-metadata-using-n

using MapControl;
using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    /// <summary>
    /// <see cref="https://exiv2.org/tags.html"/>
    /// Query1 values seems to be for JPEG and Query2 values for TIFF metadata
    /// </summary>
    internal static class ExifHandler
    {
 
        public const string FileTimeStampQuery1 = "/app1/{ushort=0}/{ushort=306}"; // String in "yyyy:MM:dd HH:mm:ss" format
        public const string FileTimeStampQuery2 = "/ifd/{ushort=306}";

        public const string DateTimeOriginalQuery1 = "/app1/{ushort=0}/{ushort=34665}/{ushort=36867}"; // String in "yyyy:MM:dd HH:mm:ss" format
        public const string DateTimeOriginalQuery2 = "/ifd/{ushort=34665}/{ushort=36867}";

        public const string DateTimeOriginalOffsetQuery1 = "/app1/{ushort=0}/{ushort=34665}/{ushort=36881}"; // +01:00 (String)
        public const string DateTimeOriginalOffsetQuery2 = "/ifd/{ushort=34665}/{ushort=36881}";
        public const string DateTimeOriginalOffsetQuery3 = "/ifd/{ushort=36881}";

        public const string ExposureTimeQuery1 = "/app1/ifd/exif/subifd:{uint=33434}"; // RATIONAL 1
        public const string ExposureTimeQuery2 = "/ifd/{ushort=34665}/{ushort=33434}"; // RATIONAL 1
        //private const string ExposureTimeQuery3 = "/app1/{ushort=0}/{ushort=34665}/{ushort=33434}"; // RATIONAL 1

        public const string LensApertureQuery1 = "/app1/ifd/exif/subifd:{uint=33437}"; // RATIONAL 1
        public const string LensApertureQuery2 = "/ifd/{ushort=34665}/{ushort=33437}"; // RATIONAL 1
        //private const string LensApertureQuery3 = "/app1/{ushort=0}/{ushort=34665}/{ushort=33437}"; // RATIONAL 1

        public const string FocalLengthQuery1 = "/app1/ifd/exif/subifd:{uint=37386}"; // RATIONAL 1
        public const string FocalLengthQuery2 = "/ifd/{ushort=34665}/{ushort=37386}"; // RATIONAL 1
        //private const string FocalLengthQuery3 = "/app1/{ushort=0}/{ushort=34665}/{ushort=37386}"; // RATIONAL 1

        public const string IsoQuery1 = "/app1/ifd/exif/subifd:{uint=34855}"; // Short
        public const string IsoQuery2 = "/ifd/{ushort=34665}/{ushort=34855}"; // Short
        //private const string IsoQuery3 = "/app1/{ushort=0}/{ushort=34665}/{ushort=34855}"; // Short

        public const string OrientationQuery1 = "/app1/{ushort=0}/{ushort=274}"; // Short
        public const string OrientationQuery2 = "/ifd/{ushort=274}"; // Short

        // North or South Latitude
        private const string GpsLatitudeRefQuery1 = "/app1/ifd/gps/subifd:{ulong=1}"; // ASCII 2
        private const string GpsLatitudeRefQuery2 = "/ifd/{ushort=34853}/{ushort=1}"; // ASCII 2
        // Latitude
        private const string GpsLatitudeQuery1 = "/app1/ifd/gps/subifd:{ulong=2}"; // RATIONAL 3
        private const string GpsLatitudeQuery2 = "/ifd/{ushort=34853}/{ushort=2}"; // RATIONAL 3
        // East or West Longitude
        private const string GpsLongitudeRefQuery1 = "/app1/ifd/gps/subifd:{ulong=3}"; // ASCII 2
        private const string GpsLongitudeRefQuery2 = "/ifd/{ushort=34853}/{ushort=3}"; // ASCII 2
        // Longitude
        private const string GpsLongitudeQuery1 = "/app1/ifd/gps/subifd:{ulong=4}"; // RATIONAL 3
        private const string GpsLongitudeQuery2 = "/ifd/{ushort=34853}/{ushort=4}"; // RATIONAL 3
        // Altitude reference 
        private const string GpsAltitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=5}"; // BYTE 1
        // Altitude 
        private const string GpsAltitudeQuery = "/app1/ifd/gps/subifd:{ulong=6}"; // RATIONAL 1

        // Relative altitude
        public const string DjiRelativeAltitude1 = @"/xmp/http\:\/\/www.dji.com\/drone-dji\/1.0\/:RelativeAltitude"; // Decimal string
        public const string DjiRelativeAltitude2 = @"/ifd/{ushort=700}/http\:\/\/www.dji.com\/drone-dji\/1.0\/:RelativeAltitude";

        private const string ExifMakerNoteQuery1 = "/app1/{ushort=0}/{ushort=34665}/{ushort=37500}";

        public const BitmapCreateOptions CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
        
        private const string ExifToolStartError = "Failed to start ExifTool";

        /// <summary> Note that the stream must still be valid when the metadata is used </summary>
        public static BitmapMetadata? LoadMetadata(Stream stream)
        {
            var decoder = BitmapDecoder.Create(stream, CreateOptions, BitmapCacheOption.OnDemand);
            if (decoder.Frames[0].Metadata is BitmapMetadata metadata)
                return metadata;
            return null;
        }

        public static string? TryGetFormat(this BitmapMetadata metadata)
        {
            try
            {
                return metadata.Format;
            }
            catch
            {
                return null;
            }
        }

        public static BitmapMetadata EncodePngMetadata(Rational? exposureTime, Location? location, DateTimeOffset? dateTaken)
        {
            var result = new BitmapMetadata("png");
            var tags = new List<string>();
            if (exposureTime is not null)
                tags.Add(string.Create(CultureInfo.InvariantCulture, $"ExposureTime={exposureTime.Numerator}/{exposureTime.Denominator}"));
            if (location is not null)
                tags.Add(string.Create(CultureInfo.InvariantCulture, $"Location={location.Latitude:+0.#####;-0.#####}{location.Longitude:+0.#####;-0.#####}"));
            if (tags.Count > 0)
                result.SetQuery("/Text/Description", string.Join(";", tags));
            if (dateTaken.HasValue)
                SetDateTaken(result, dateTaken.Value);
            return result;
        }

        static (Rational? ExposureTime, Location? Location)  DecodePngMetadata(BitmapMetadata metadata)
        {
            if (metadata.GetQuery("/Text/Description") is not string str)
                return (null, null);
            Rational? exposureTime = null;
            Location? location = null;
            var tags = str.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var tag in tags)
            {
                var parts = tag.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    if (parts[0] == "ExposureTime")
                        exposureTime = Rational.Decode(parts[1]);
                    else if (parts[0] == "Location")
                    {
                        var i = parts[1].IndexOfAny(['+', '-'], 1);
                        if (i > 0 &&
                            double.TryParse(parts[1].AsSpan(0, i), CultureInfo.InvariantCulture, out var latitude) &&
                            double.TryParse(parts[1].AsSpan(i), CultureInfo.InvariantCulture, out var longitude))
                            location = new Location(latitude, longitude);
                    }
                }
            }
            return (exposureTime, location);
        }

        public static BitmapMetadata? CreateMetadataForEncoder(BitmapMetadata source, BitmapEncoder encoder)
        {
            try
            {
                BitmapMetadata result;
                int setValue = 0;

                void TransferValue(string query1, string query2, string? query3 = null)
                {
                    var value = source.GetQuery(query1) ?? source.GetQuery(query2);
                    if (value is null && query3 is not null)
                        value = source.GetQuery(query3);
                    if (value is not null)
                        try
                        {
                            if (setValue == 1)
                                result.SetQuery(query1, value);
                            else if (setValue == 2)
                                result.SetQuery(query2, value);
                        }
                        catch { } // Ignore unsupported properties
                }

                if (encoder is JpegBitmapEncoder)
                {
                    if (source.TryGetFormat() == "jpg")
                        return source;
                    result = new BitmapMetadata("jpg");
                    setValue = 1;

                }
                else if (encoder is WmpBitmapEncoder)
                {
                    if (source.TryGetFormat() == "wmphoto")
                        return source;
                    result = new BitmapMetadata("wmphoto");
                    setValue = 1;

                }
                else if (encoder is TiffBitmapEncoder)
                {
                    if (source.TryGetFormat() == "tiff")
                        return source;
                    result = new BitmapMetadata("tiff");
                    setValue = 2;
                }
                else if (encoder is PngBitmapEncoder)
                {
                    if (source.TryGetFormat() == "png")
                        return source;
                    result = new BitmapMetadata("png");
                    var exposureTime = Rational.Decode(source.GetQuery(ExposureTimeQuery1) ?? source.GetQuery(ExposureTimeQuery2));
                    var location = GetGeotag(source);
                    return EncodePngMetadata(exposureTime, location, DecodeTimeStamp(source, null));
                }
                else
                    return null;

                var dateTaken = DecodeTimeStamp(source, null);
                if (dateTaken.HasValue)
                    SetDateTaken(result, dateTaken.Value);

                if (source.TryGetFormat() == "png")
                {
                    var (exposureTime, location) = DecodePngMetadata(source);
                    if (exposureTime is not null)
                    {
                        if (setValue == 1)
                            result.SetQuery(ExposureTimeQuery1, exposureTime.Bytes);
                        else if (setValue == 2)
                            result.SetQuery(ExposureTimeQuery2, exposureTime.Bytes);
                    }
                    if (location is not null)
                        SetGeotag(result, location);
                    return result;
                }

                try
                {
                    if (!string.IsNullOrEmpty(source.CameraManufacturer))
                        result.CameraManufacturer = source.CameraManufacturer;
                    if (!string.IsNullOrEmpty(source.CameraModel))
                        result.CameraModel = source.CameraModel;
                    if (!string.IsNullOrEmpty(source.Title))
                        result.Title = source.Title;
                    if (!string.IsNullOrEmpty(source.Comment))
                        result.Comment = source.Comment;
                    if (!string.IsNullOrEmpty(source.Copyright))
                        result.Copyright = source.Copyright;
                    if (string.IsNullOrEmpty(source.ApplicationName))
                        result.ApplicationName = nameof(PhotoLocator);
                    else
                        result.ApplicationName = nameof(PhotoLocator) + ", " + source.ApplicationName;
                }
                catch (NotSupportedException) { }

                TransferValue(DateTimeOriginalQuery1, DateTimeOriginalQuery2);
                TransferValue(DateTimeOriginalOffsetQuery1, DateTimeOriginalOffsetQuery2, DateTimeOriginalOffsetQuery3);
                TransferValue(ExposureTimeQuery1, ExposureTimeQuery2);
                TransferValue(LensApertureQuery1, LensApertureQuery2);
                TransferValue(FocalLengthQuery1, FocalLengthQuery2);
                TransferValue(IsoQuery1, IsoQuery2);
                TransferValue(GpsLatitudeQuery1, GpsLatitudeQuery2);
                TransferValue(GpsLongitudeQuery1, GpsLongitudeQuery2);
                TransferValue(GpsLatitudeRefQuery1, GpsLatitudeRefQuery2);
                TransferValue(GpsLongitudeRefQuery1, GpsLongitudeRefQuery2);
                TransferValue(DjiRelativeAltitude1, DjiRelativeAltitude2);

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static void SetDateTaken(BitmapMetadata metadata, DateTimeOffset dateTaken)
        {
            try
            {   // Parsing the timestamp string should use current culture but writing must happen in invariant culture
                metadata.DateTaken = dateTaken.LocalDateTime.ToString(CultureInfo.InvariantCulture);
            }
            catch { }
        }

        public static MemoryStream SetJpegMetadata(Stream source, BitmapMetadata metadata)
        {
            // Decode
            var decoder = BitmapDecoder.Create(source, CreateOptions, BitmapCacheOption.None); // Caching needs to be None for lossless setting metadata
            var frame = decoder.Frames[0];

            // Encode with metadata
            var encoder = new JpegBitmapEncoder();
            var jpegMetadata = CreateMetadataForEncoder(metadata, encoder) ?? throw new NotSupportedException("Unsupported metadata format");
            encoder.Frames.Add(BitmapFrame.Create(frame, frame.Thumbnail, jpegMetadata, frame.ColorContexts));
            var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);

            // Check
            memoryStream.Position = 0;
            CheckPixels(frame, BitmapDecoder.Create(memoryStream, CreateOptions, BitmapCacheOption.None).Frames[0]);

            memoryStream.Position = 0;
            return memoryStream;
        }

        public static void SetJpegGeotag(string sourceFileName, string targetFileName, Location location)
        {
            Log.Write($"Updating '{targetFileName}' using JpegBitmapEncoder");
            using var memoryStream = new MemoryStream();
            using (var originalFileStream = File.OpenRead(sourceFileName))
            {
                // Decode
                var sourceSize = originalFileStream.Length;
                var decoder = BitmapDecoder.Create(originalFileStream, CreateOptions, BitmapCacheOption.None); // Caching needs to be None for lossless setting metadata
                var frame = decoder.Frames[0];

                // Tag
                var metadata = frame.Metadata is null ? new BitmapMetadata("jpg") : (BitmapMetadata)frame.Metadata.Clone();
                SetGeotag(metadata, location);

                // Encode
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame, frame.Thumbnail, metadata, frame.ColorContexts));
                encoder.Save(memoryStream);

                // Check
                memoryStream.Position = 0;
                CheckPixels(frame, BitmapDecoder.Create(memoryStream, CreateOptions, BitmapCacheOption.None).Frames[0]);
            }
            // Save
            using var targetFileStream = File.Open(targetFileName, FileMode.Create, FileAccess.Write);
            memoryStream.Position = 0;
            memoryStream.CopyTo(targetFileStream);
        }

        public static async Task SetGeotagAsync(string sourceFileName, string targetFileName, Location location, string? exifToolPath, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(exifToolPath))
            {
                SetJpegGeotag(sourceFileName, targetFileName, location);
                return;
            }

            var startInfo = new ProcessStartInfo(exifToolPath,
                //"-m " + // Ignore minor errors and warnings
                $"-GPSLatitude={location.Latitude.ToString(CultureInfo.InvariantCulture)} " +
                $"-GPSLatitudeRef={Math.Sign(location.Latitude)} " +
                $"-GPSLongitude={location.Longitude.ToString(CultureInfo.InvariantCulture)} " +
                $"-GPSLongitudeRef={Math.Sign(location.Longitude)} " +
                $"\"{sourceFileName}\" ");
            if (targetFileName == sourceFileName)
                startInfo.Arguments += "-overwrite_original";
            else
            {
                File.Delete(targetFileName);
                startInfo.Arguments += $"-out \"{targetFileName}\"";
            }
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
            var output = await process.StandardOutput.ReadToEndAsync(ct) + '\n' + await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            Log.Write(output);
            if (process.ExitCode != 0)
                throw new UserMessageException(output);
        }

        public static async Task AdjustTimeStampAsync(string sourceFileName, string targetFileName, string offset, string exifToolPath, CancellationToken ct)
        {
            var sign = offset[0];
            offset = offset[1..];
            var startInfo = new ProcessStartInfo(exifToolPath, $"\"-AllDates{sign}={offset}\" \"{sourceFileName}\" ");
            if (targetFileName == sourceFileName)
                startInfo.Arguments += "-overwrite_original";
            else
            {
                File.Delete(targetFileName);
                startInfo.Arguments += $"-out \"{targetFileName}\"";
            }
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
            var output = await process.StandardOutput.ReadToEndAsync(ct) + '\n' + await process.StandardError.ReadToEndAsync(ct);  // We must read before waiting
            await process.WaitForExitAsync(ct);
            Log.Write(output);
            if (process.ExitCode != 0)
                throw new UserMessageException(output);
        }

        private static void CheckPixels(BitmapFrame frame1, BitmapFrame frame2)
        {
            if (frame1.PixelWidth != frame2.PixelWidth || frame1.PixelHeight != frame2.PixelHeight)
                throw new InvalidDataException("Dimensions have changed");

            var bytesPerLine = frame1.PixelWidth * 3;
            var pixels1 = new byte[bytesPerLine * frame1.PixelHeight];
            frame1.CopyPixels(pixels1, bytesPerLine, 0);

            var pixels2 = new byte[bytesPerLine * frame2.PixelHeight];
            frame2.CopyPixels(pixels2, bytesPerLine, 0);

            if (!pixels1.SequenceEqual(pixels2))
                throw new InvalidDataException("Pixels have changed");
        }

        public static void SetGeotag(BitmapMetadata metadata, Location location)
        {
            // Pad the metadata so that it can be expanded with new tags. Is this ever necessary?
            //const uint PaddingAmount = 64;
            //metadata.SetQuery("/app1/ifd/PaddingSchema:Padding", PaddingAmount);
            //metadata.SetQuery("/app1/ifd/exif/PaddingSchema:Padding", PaddingAmount);
            //metadata.SetQuery("/xmp/PaddingSchema:Padding", PaddingAmount);

            var latitudeRational = new GPSRational(location.Latitude);
            var longitudeRational = new GPSRational(location.Longitude);
            metadata.SetQuery(GpsLatitudeQuery1, latitudeRational.Bytes);
            metadata.SetQuery(GpsLongitudeQuery1, longitudeRational.Bytes);
            metadata.SetQuery(GpsLatitudeRefQuery1, location.Latitude >= 0 ? "N" : "S");
            metadata.SetQuery(GpsLongitudeRefQuery1, location.Longitude >= 0 ? "E" : "W");

            //Rational altitudeRational = new Rational((int)altitude, 1);  //denominator = 1 for Rational
            //metadata.SetQuery(GpsAltitudeQuery, altitudeRational.bytes);
        }

        public static Location? GetGeotag(string sourceFileName)
        {
            using var file = File.OpenRead(sourceFileName);
            var metadata = LoadMetadata(file);
            return GetGeotag(metadata);
        }

        public static Location? GetGeotag(BitmapMetadata? metadata)
        {
            if (metadata is null)
                return null;

            if (metadata.TryGetFormat() == "png")
            {
                var (_, location) = DecodePngMetadata(metadata);
                return location;
            }

            if ((metadata.GetQuery(GpsLatitudeRefQuery1) ?? metadata.GetQuery(GpsLatitudeRefQuery2)) is not string latitudeRef)
                return null;
            var latitude = GPSRational.Decode(metadata.GetQuery(GpsLatitudeQuery1) ?? metadata.GetQuery(GpsLatitudeQuery2));
            if (latitude is null)
                return null;
            if (latitudeRef == "S")
                latitude.AngleInDegrees = -latitude.AngleInDegrees;

            if ((metadata.GetQuery(GpsLongitudeRefQuery1) ?? metadata.GetQuery(GpsLongitudeRefQuery2)) is not string longitudeRef)
                return null;
            var longitude = GPSRational.Decode(metadata.GetQuery(GpsLongitudeQuery1) ?? metadata.GetQuery(GpsLongitudeQuery2));
            if (longitude is null)
                return null;
            if (longitudeRef == "W")
                longitude.AngleInDegrees = -longitude.AngleInDegrees;

            if (latitude.AngleInDegrees == 0 && longitude.AngleInDegrees == 0)
                return null;

            //byte[] altitude = (byte[])metadata.GetQuery(GpsAltitudeQuery);

            return new Location(latitude: latitude.AngleInDegrees, longitude: longitude.AngleInDegrees);
        }

        public static DateTimeOffset? DecodeTimeStamp(BitmapMetadata metadata, Stream? imageStream)
        {
            try
            {
                var timestampStr = (metadata.GetQuery(DateTimeOriginalQuery1) ?? metadata.GetQuery(DateTimeOriginalQuery2)
                    ?? metadata.GetQuery(FileTimeStampQuery1) ?? metadata.GetQuery(FileTimeStampQuery2)) as string;

                if (!DateTime.TryParseExact(timestampStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timeStamp) &&
                    !DateTime.TryParse(metadata.DateTaken, out timeStamp))
                    return null;

                if (metadata.CameraManufacturer == "DJI") // Fix for DNG and JPG version of the same picture having different metadata.DateTaken
                {
                    var fileTimeStampStr = (metadata.GetQuery(FileTimeStampQuery1) ?? metadata.GetQuery(FileTimeStampQuery2)) as string;
                    if (DateTime.TryParseExact(fileTimeStampStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var fileTimeStamp))
                    {
                        var diff = timeStamp - fileTimeStamp;
                        if (diff.Duration() < TimeSpan.FromSeconds(10)) // Only take fileTimeStamp if the file wasn't changed in another program
                            timeStamp = fileTimeStamp;
                    }
                }

                var offsetStr = (metadata.GetQuery(DateTimeOriginalOffsetQuery1) ?? metadata.GetQuery(DateTimeOriginalOffsetQuery2) ?? metadata.GetQuery(DateTimeOriginalOffsetQuery3)) as string;
                if (TimeSpan.TryParseExact(offsetStr, @"\+hh\:mm", CultureInfo.InvariantCulture, out var offset))
                    return new DateTimeOffset(timeStamp, offset);
                if (TimeSpan.TryParseExact(offsetStr, @"\-hh\:mm", CultureInfo.InvariantCulture, out offset))
                    return new DateTimeOffset(timeStamp, -offset);
                if (imageStream is not null && metadata.CameraManufacturer == "Canon")
                {
                    var makerNotes = metadata.GetQuery(ExifMakerNoteQuery1) as BitmapMetadataBlob;
                    if (makerNotes is not null)
                    {
                        using var ifdDecoder = new IfdDecoder(new MemoryStream(makerNotes.GetBlobValue(), false), 0);
                        foreach (var tag in ifdDecoder.EnumerateIfdTags())
                            if (tag.TagId == 0x35 && tag.ValueCount == 4) // Canon time zone tag
                            {
                                using var tagDecoder = new IfdDecoder(imageStream, 12);
                                var timeZone = tagDecoder.DecodeUInt32Tag(tag);
                                offset = TimeSpan.FromMinutes((Int16)(timeZone[1]));
                                if (offset.TotalHours < 13)
                                    return new DateTimeOffset(timeStamp, offset);
                            }
                    }
                }

                return DateTime.SpecifyKind(timeStamp, DateTimeKind.Local);
            }
            catch (NotSupportedException) { } // Fallback to DateTaken if metadata query is not supported

            return DateTime.TryParse(metadata.DateTaken, out var dateTakenStr) ? DateTime.SpecifyKind(dateTakenStr, DateTimeKind.Local) : null;
        }

        public static double? GetRelativeAltitude(BitmapMetadata metadata)
        {
            var altitudeString = (metadata.GetQuery(DjiRelativeAltitude1) ?? metadata.GetQuery(DjiRelativeAltitude2)) as string;
            if (double.TryParse(altitudeString, NumberStyles.Number, CultureInfo.InvariantCulture, out var altitude))
                return altitude;
            return null;
        }

        public static double? GetGpsAltitude(BitmapMetadata metadata)
        {
            var altitudeRef = metadata.GetQuery(GpsAltitudeRefQuery) as byte?;
            if (altitudeRef is null)
                return null;
            var altitude = Rational.Decode(metadata.GetQuery(GpsAltitudeQuery));
            if (altitude is null)
                return null;
            return altitudeRef == 1 ? -altitude.ToDouble() : altitude.ToDouble();
        }

        public static IEnumerable<string> EnumerateMetadata(string fileName, string? exifToolPath)
        {
            try
            {
                using var file = File.OpenRead(fileName);
                var metadata = LoadMetadata(file) ?? throw new UserMessageException("Unable to list metadata for file");
                if (metadata.Any())
                    return EnumerateMetadata(metadata, string.Empty, file).ToArray();
                if (metadata.GetQuery("/ifd") is BitmapMetadata ifd)
                    return EnumerateMetadata(ifd, "/ifd", file).ToArray();
                throw new UserMessageException("Unable to list metadata for file");
            }
            catch (Exception ex) when (ex is UserMessageException or NotSupportedException)
            {
                if (string.IsNullOrEmpty(exifToolPath))
                    throw;
                return EnumerateMetadataUsingExifTool(fileName, exifToolPath);
            }
        }

        public static IEnumerable<string> EnumerateMetadata(BitmapMetadata metadata, string query, Stream imageStream)
        {
            foreach (string relativeQuery in metadata)
            {
                string fullQuery = query + relativeQuery;
                object? metadataValue;
                try
                {
                    metadataValue = metadata.GetQuery(relativeQuery);
                }
                catch (NotSupportedException)
                {
                    continue;
                }
                if (metadataValue is BitmapMetadata innerBitmapMetadata)
                    foreach (var inner in EnumerateMetadata(innerBitmapMetadata, fullQuery, imageStream))
                        yield return inner;
                else if (metadataValue is long or ulong)
                {
                    var rational = Rational.Decode(metadataValue);
                    if (rational != null && rational.Denominator > 0)
                        yield return fullQuery + $" = {metadataValue} ({rational.Numerator} / {rational.Denominator})";
                    else
                        yield return fullQuery + $" = {metadataValue} ({metadataValue.GetType().Name})";
                }
                else if (metadataValue is Array arrayValue)
                {
                    if (arrayValue.Length <= 4)
                    {
                        var str = new StringBuilder();
                        foreach (var element in arrayValue)
                            str.Append(element.ToString() + ' ');
                        yield return fullQuery + $" = {str}({metadataValue.GetType().Name})";
                    }
                    else
                        yield return fullQuery + $" = {metadataValue.GetType().Name} with {arrayValue.Length} elements";
                }
                else if (metadataValue is BitmapMetadataBlob blob)
                {
                    var value = blob.GetBlobValue();
                    if (value.Length <= 10)
                    {
                        yield return fullQuery + $" = [{string.Join(", ", value.Select(a => a.ToString(CultureInfo.InvariantCulture)))}] ({metadataValue.GetType().Name})";
                    }
                    else 
                    {
                        yield return fullQuery + $" = {value.Length} bytes ({metadataValue.GetType().Name})";
                        if (fullQuery == ExifMakerNoteQuery1)
                        {
                            using var ifdDecoder = new IfdDecoder(new MemoryStream(value, false), 0);
                            using var tagDecoder = new IfdDecoder(imageStream, 12);
                            foreach (var tag in ifdDecoder.EnumerateIfdTags())
                            {
                                if (tag.FieldType == IfdDecoder.FieldType.Ascii)
                                    yield return fullQuery + $"/{tag.TagId} = {tag.FieldType}*{tag.ValueCount} '{tagDecoder.DecodeStringTag(tag)}'";
                                else if (tag.FieldType == IfdDecoder.FieldType.Long && tag.ValueCount > 0 && tag.ValueCount < 100)
                                    yield return fullQuery + $"/{tag.TagId} = {tag.FieldType} {string.Join(", ", tagDecoder.DecodeUInt32Tag(tag).Select(a => a.ToString(CultureInfo.InvariantCulture)))}";
                                else
                                    yield return fullQuery + $"/{tag.TagId} = {tag.FieldType} {tag.ValueCount} * {tag.ValueOrOffset}";
                            }
                        }
                    }
                }
                else if (metadataValue != null)
                    yield return fullQuery + $" = {metadataValue} ({metadataValue.GetType().Name})";
            }
        }

        public static string[] EnumerateMetadataUsingExifTool(string fileName, string exifToolPath)
        {
            var startInfo = new ProcessStartInfo(exifToolPath, $"-s2 -c +%.6f \"{fileName}\""); // -H to add hexadecimal values
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public static string GetMetadataString(BitmapMetadata metadata, Stream? imageStream)
        {
            return GetMetadataString(metadata, DecodeTimeStamp(metadata, imageStream));
        }

        private static string GetMetadataString(BitmapMetadata metadata, DateTimeOffset? timeStamp)
        {
            var metadataStrings = new List<string>();

            try
            {
                if (!string.IsNullOrEmpty(metadata.CameraModel))
                    metadataStrings.Add(metadata.CameraModel.Trim());
            }
            catch (NotSupportedException) { }

            var altitude = GetRelativeAltitude(metadata);
            if (altitude.HasValue)
                metadataStrings.Add(altitude.Value.ToString("0.0", CultureInfo.CurrentCulture) + 'm');

            var exposureTime = Rational.Decode(metadata.GetQuery(ExposureTimeQuery1) ?? metadata.GetQuery(ExposureTimeQuery2));
            if (exposureTime is null && metadata.TryGetFormat() == "png")
                (exposureTime, var _) = DecodePngMetadata(metadata);
            if (exposureTime != null)
            {
                if (exposureTime.Numerator == 1 && exposureTime.Denominator > 1)
                    metadataStrings.Add($"{exposureTime.Numerator}/{exposureTime.Denominator}s");
                else
                    metadataStrings.Add(exposureTime.ToDouble() + "s");
            }

            var lensAperture = Rational.Decode(metadata.GetQuery(LensApertureQuery1) ?? metadata.GetQuery(LensApertureQuery2));
            if (lensAperture != null && lensAperture.Numerator > 0 && lensAperture.Denominator > 0)
                metadataStrings.Add("f/" + lensAperture.ToDouble());

            var focalLength = Rational.Decode(metadata.GetQuery(FocalLengthQuery1) ?? metadata.GetQuery(FocalLengthQuery2));
            if (focalLength != null && focalLength.Numerator > 0 && focalLength.Denominator > 0)
                metadataStrings.Add(focalLength.ToDouble() + "mm");

            var iso = metadata.GetQuery(IsoQuery1) ?? metadata.GetQuery(IsoQuery2);
            if (iso != null)
                metadataStrings.Add("ISO" + iso.ToString());

            if (timeStamp.HasValue)
                metadataStrings.Add(FormatTimestampForDisplay(timeStamp.Value));

            return string.Join(", ", metadataStrings);
        }

        private static string FormatTimestampForDisplay(DateTimeOffset timestamp)
        {
            return timestamp.ToString(CultureInfo.CurrentCulture);
        }

        public static async Task<(Location? Location, DateTimeOffset? TimeStamp, string Metadata, Rotation Orientation)> DecodeMetadataAsync(string fileName, bool isVideo, string? exifToolPath, CancellationToken ct)
        {
            if (isVideo && !string.IsNullOrEmpty(exifToolPath))
                return DecodeMetadataUsingExifTool(fileName, exifToolPath);
            try
            {
                using var file = await FileHelpers.OpenFileWithRetryAsync(fileName, ct);
                var metadata = LoadMetadata(file);
                if (metadata is null)
                    return (null, null, string.Empty, Rotation.Rotate0);
                var orientationStr = metadata.GetQuery(OrientationQuery1) as ushort? ?? metadata.GetQuery(OrientationQuery2) as ushort? ?? 0;
                var orientation = orientationStr switch
                {
                    3 => Rotation.Rotate180,
                    6 => Rotation.Rotate90,
                    8 => Rotation.Rotate270,
                    _ => Rotation.Rotate0
                };
                var timeStamp = DecodeTimeStamp(metadata, file);
                return (GetGeotag(metadata), timeStamp, GetMetadataString(metadata, timeStamp), orientation);
            }
            catch (NotSupportedException)
            {
                if (string.IsNullOrEmpty(exifToolPath))
                    throw;
                return DecodeMetadataUsingExifTool(fileName, exifToolPath);
            }
        }

        public static Dictionary<string, string> LoadMetadataUsingExifTool(string fileName, string exifToolPath)
        {
            return DecodeExifToolMetadataToDictionary(EnumerateMetadataUsingExifTool(fileName, exifToolPath));
        }

        public static (Location? Location, DateTimeOffset? TimeStamp, string Metadata, Rotation Orientation) DecodeMetadataUsingExifTool(string fileName, string exifToolPath)
        {
            var metadata = LoadMetadataUsingExifTool(fileName, exifToolPath);

            var metadataStrings = new List<string>();
            if (metadata.TryGetValue("Model", out var value) || metadata.TryGetValue("Encoder", out value))
                metadataStrings.Add(value);
            if (metadata.TryGetValue("ExposureTime", out value) && value != "undef")
                metadataStrings.Add(value + "s");
            if (metadata.TryGetValue("ApertureValue", out value))
                metadataStrings.Add("f/" + value);
            if (metadata.TryGetValue("FocalLength", out value))
                metadataStrings.Add(value);
            if (metadata.TryGetValue("ISO", out value))
                metadataStrings.Add("ISO" + value);
            if (metadata.TryGetValue("ImageSize", out value))
                metadataStrings.Add(value);
            if (metadata.TryGetValue("VideoFrameRate", out value) && value != "1")
            {
                metadataStrings.Add(value + "fps");
                if (metadata.TryGetValue("Duration", out value))
                    metadataStrings.Add(value);
                if (metadata.TryGetValue("AvgBitrate", out value))
                    metadataStrings.Add(value);
            }

            var creationTimestamp = DecodeTimeStampFromExifTool(metadata);
            if (creationTimestamp.HasValue)
                metadataStrings.Add(FormatTimestampForDisplay(creationTimestamp.Value));
            var location = DecodeLocationFromExifTool(metadata);
            var orientation = DecodeOrientationFromExifTool(metadata);

            return (location, creationTimestamp, string.Join(", ", metadataStrings), orientation);
        }

        internal static Dictionary<string, string> DecodeExifToolMetadataToDictionary(string[] lines)
        {
            var metadata = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var index = line.IndexOf(':', StringComparison.Ordinal);
                if (index < line.Length - 2)
                    metadata[line[..index]] = line[(index + 2)..];
            }
            return metadata;
        }

        internal static DateTimeOffset? DecodeTimeStampFromExifTool(Dictionary<string, string> metadata)
        {
            if (!metadata.TryGetValue("SubSecCreateDate", out var timeStampStr) &&
                !metadata.TryGetValue("SubSecDateTimeOriginal", out timeStampStr) &&
                !metadata.TryGetValue("CreationDate", out timeStampStr) &&
                !metadata.TryGetValue("CreateDate", out timeStampStr) &&
                !metadata.TryGetValue("DateTimeOriginal", out timeStampStr))
                return null;
            var fractionStart = timeStampStr.IndexOf('.', StringComparison.Ordinal);
            if (fractionStart > 0)
            {
                var fractionEnd = timeStampStr.IndexOfAny(['+', '-'], fractionStart);
                if (fractionEnd > 0)
                    timeStampStr = string.Concat(timeStampStr.AsSpan(0, fractionStart), timeStampStr.AsSpan(fractionEnd));
                else
                    timeStampStr = timeStampStr[..fractionStart];
            }
            if (DateTimeOffset.TryParseExact(timeStampStr, "yyyy:MM:dd HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timestampOffset))
                return timestampOffset;
            if (metadata.TryGetValue("OffsetTimeOriginal", out var offsetStr) ||
                metadata.TryGetValue("OffsetTime", out offsetStr) ||
                metadata.TryGetValue("TimeZone", out offsetStr))
            {
                var timeStampWithOffsetStr = timeStampStr + offsetStr;
                if (DateTimeOffset.TryParseExact(timeStampWithOffsetStr, "yyyy:MM:dd HH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out timestampOffset))
                    return timestampOffset;
            }
            var timeZone = metadata.TryGetValue("FileType", out var fileType) && fileType=="JPEG" ? DateTimeStyles.AssumeLocal : DateTimeStyles.AssumeUniversal;
            if (DateTime.TryParseExact(timeStampStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | timeZone, out var timestamp))
                return timestamp;
            return null;
        }

        private static Rotation DecodeOrientationFromExifTool(Dictionary<string, string> metadata)
        {
            if (!metadata.TryGetValue("Orientation", out var orientationStr))
                return Rotation.Rotate0;
            return orientationStr switch
            {
                "Rotate 90 CW" or "6" => Rotation.Rotate90,
                "Rotate 180" or "3" => Rotation.Rotate180,
                "Rotate 270 CW" or "8" => Rotation.Rotate270,
                _ => Rotation.Rotate0
            };
        }

        private static Location? DecodeLocationFromExifTool(Dictionary<string, string> metadata)
        {
            if (metadata.TryGetValue("GPSPosition", out var locationStr) ||
                metadata.TryGetValue("GPSCoordinates", out locationStr))
            {
                var parts = locationStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[0], CultureInfo.InvariantCulture, out var latitude) &&
                    double.TryParse(parts[2], CultureInfo.InvariantCulture, out var longitude))
                    return new Location(latitude, longitude);
            }
            return null;
        }

        public static async Task TransferMetadataAsync(string metadataFileName, string sourceFileName, string targetFileName, string exifToolPath, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo(exifToolPath, $"-tagsfromfile \"{metadataFileName}\" \"{sourceFileName}\" "); // -exif 
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            if (targetFileName == sourceFileName)
                startInfo.Arguments += "-overwrite_original";
            else
            {
                File.Delete(targetFileName);
                startInfo.Arguments += $"-out \"{targetFileName}\"";
            }
            var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
            var output = await process.StandardOutput.ReadToEndAsync(ct) + '\n' + await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            Log.Write(output);
            if (process.ExitCode != 0)
                throw new UserMessageException("Failed to transfer metadata: " + output);
        }
    }
}