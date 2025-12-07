using MapControl;
using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata;

static class ExifTool
{
    private const string ExifToolStartError = "Failed to start ExifTool";

    public static async Task AdjustTimestampAsync(string sourceFileName, string targetFileName, string offset, string exifToolPath, CancellationToken ct)
    {
        if (offset is null || offset.Length < 2)
            throw new UserMessageException("Offset must have a sign followed by a value");
        var sign = offset[0];
        offset = offset[1..];
        var startInfo = new ProcessStartInfo(exifToolPath, $"\"-AllDates{sign}={offset}\" \"{sourceFileName}\" ");
        await RunExifToolAsync(sourceFileName, targetFileName, startInfo, ct);
    }

    public static async Task SetTimestampAsync(string sourceFileName, string targetFileName, string timestamp, string exifToolPath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(exifToolPath, $"\"-AllDates={timestamp}\" \"{sourceFileName}\" ");
        await RunExifToolAsync(sourceFileName, targetFileName, startInfo, ct);
    }

    public static async Task TransferMetadataAsync(string metadataFileName, string sourceFileName, string targetFileName, string exifToolPath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(exifToolPath, $"-tagsfromfile \"{metadataFileName}\" \"{sourceFileName}\" "); // -exif 
        await RunExifToolAsync(sourceFileName, targetFileName, startInfo, ct);
    }

    public static async Task SetGeotagAsync(string sourceFileName, string targetFileName, Location location, string? exifToolPath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(exifToolPath))
        {
            ExifHandler.SetJpegGeotag(sourceFileName, targetFileName, location);
            return;
        }

        var startInfo = new ProcessStartInfo(exifToolPath, string.Create(CultureInfo.InvariantCulture,
            //"-m " + // Ignore minor errors and warnings
            $"-GPSLatitude={location.Latitude} " +
            $"-GPSLatitudeRef={Math.Sign(location.Latitude)} " +
            $"-GPSLongitude={location.Longitude} " +
            $"-GPSLongitudeRef={Math.Sign(location.Longitude)} " +
            $"\"{sourceFileName}\" "));
        await RunExifToolAsync(sourceFileName, targetFileName, startInfo, ct);
    }

    private static async Task RunExifToolAsync(string sourceFileName, string targetFileName, ProcessStartInfo startInfo, CancellationToken ct)
    {
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
        using var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
        var output = await process.StandardOutput.ReadToEndAsync(ct) + '\n' + await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Log.Write(output);
        if (process.ExitCode != 0)
            throw new UserMessageException(output);
    }

    public static string[] EnumerateMetadata(string fileName, string exifToolPath)
    {
        var startInfo = new ProcessStartInfo(exifToolPath, $"-s2 -c +%.6f \"{fileName}\""); // -H to add hexadecimal values
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        using var process = Process.Start(startInfo) ?? throw new IOException(ExifToolStartError);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static Dictionary<string, string> LoadMetadata(string fileName, string exifToolPath)
    {
        return DecodeMetadataToDictionary(EnumerateMetadata(fileName, exifToolPath));
    }

    internal static Dictionary<string, string> DecodeMetadataToDictionary(string[] lines)
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

    public static (Location? Location, DateTimeOffset? TimeStamp, string Metadata, Rotation Orientation) DecodeMetadata(string fileName, string exifToolPath)
    {
        var metadata = LoadMetadata(fileName, exifToolPath);

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

        var creationTimestamp = DecodeTimeStamp(metadata);
        if (creationTimestamp.HasValue)
            metadataStrings.Add(ExifHandler.FormatTimestampForDisplay(creationTimestamp.Value));
        var location = DecodeLocation(metadata);
        var orientation = DecodeOrientation(metadata);

        return (location, creationTimestamp, string.Join(", ", metadataStrings), orientation);
    }

    internal static DateTimeOffset? DecodeTimeStamp(Dictionary<string, string> metadata)
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
        var timeZone = metadata.TryGetValue("FileType", out var fileType) && fileType == "JPEG" ? DateTimeStyles.AssumeLocal : DateTimeStyles.AssumeUniversal;
        if (DateTime.TryParseExact(timeStampStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | timeZone, out var timestamp))
            return timestamp;
        return null;
    }

    private static Rotation DecodeOrientation(Dictionary<string, string> metadata)
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

    private static Location? DecodeLocation(Dictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("GPSPosition", out var locationStr) ||
            metadata.TryGetValue("GPSCoordinates", out locationStr))
        {
            var parts = locationStr.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                double.TryParse(parts[0], CultureInfo.InvariantCulture, out var latitude) &&
                double.TryParse(parts[2], CultureInfo.InvariantCulture, out var longitude))
            {
                if (parts[1] is "S" or "-")
                    latitude = -latitude;
                if (parts[3] is "W" or "-")
                    longitude = -longitude;
                return new Location(latitude, longitude);
            }
        }
        return null;
    }
}
