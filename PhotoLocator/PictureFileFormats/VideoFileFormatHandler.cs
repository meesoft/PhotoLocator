using MapControl;
using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class VideoFileFormatHandler
    {
        public static (BitmapSource, DateTimeOffset?, Location?, string?) LoadFromFile(string fullPath, int maxWidth, string? skipTo, ISettings settings, CancellationToken ct)
        {
            BitmapSource? result = null;
            string? metadata = null, duration = null;
            Location? location = null;
            DateTime? timeStamp = null;
            var videoTransforms = new VideoProcessing(settings);
            var args = 
                (string.IsNullOrEmpty(skipTo) ? null : $"-ss {skipTo} ") +
                $"-i \"{fullPath}\" "+
                (maxWidth == int.MaxValue ? null : $"-vf \"scale={maxWidth}:-1\" ") +
                "-frames:0 1";

            Task.Run(() => videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args,
                frame => result = frame,
                line =>
                {
                    Log.Write(line);
                    if (timeStamp is null && line.Contains("  creation_time", StringComparison.Ordinal))
                    {
                        var i = line.IndexOf(':', StringComparison.Ordinal);
                        if (i > 0 && DateTime.TryParse(line.AsSpan(i + 1), CultureInfo.InvariantCulture, DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite, out var dt))
                            timeStamp = dt;
                    }
                    else if (location is null && line.Contains("  location", StringComparison.Ordinal))
                    {
                        var i1 = line.IndexOfAny(['-', '+']);
                        if (i1 > 0)
                        {
                            var i2 = line.IndexOfAny(['-', '+'], i1 + 2);
                            if (i2 > 0 &&
                                double.TryParse(line.AsSpan(i1, i2 - i1), CultureInfo.InvariantCulture, out var latitude) &&
                                double.TryParse(line.AsSpan(i2).TrimEnd('/'), CultureInfo.InvariantCulture, out var longitude))
                                location = new Location(latitude, longitude);
                        }
                    }
                    else if (duration is null && line.StartsWith(VideoProcessing.DurationOutputPrefix, StringComparison.Ordinal))
                    {
                        var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            duration = parts[1];
                    }
                    else if (metadata is null && line.StartsWith(VideoProcessing.EncodingOutputPrefix, StringComparison.Ordinal))
                    {
                        var i = Math.Max(VideoProcessing.EncodingOutputPrefix.Length,
                            line.IndexOf(": ", VideoProcessing.EncodingOutputPrefix.Length, StringComparison.Ordinal));
                        metadata = (duration is null ? null : duration + ",") + line[(i + 1)..];
                    }
                }, ct), ct).GetAwaiter().GetResult();
            return (result ?? throw new FileFormatException(), timeStamp, location, metadata);
        }
    }
}
