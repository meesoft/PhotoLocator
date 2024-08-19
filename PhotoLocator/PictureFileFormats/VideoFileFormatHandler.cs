using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class VideoFileFormatHandler
    {
        public static (BitmapSource, string?) LoadFromFile(string fullPath, int maxWidth, ISettings settings, CancellationToken ct)
        {
            var videoTransforms = new VideoTransforms(settings);
            var vf = maxWidth < int.MaxValue ? $"-vf \"scale={maxWidth}:-1\"" : "";
            var args = $"-i \"{fullPath}\" {vf} -frames:0 1";
            BitmapSource? result = null;
            string? metadata = null, duration = null;
            Task.Run(() => videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, 
                frame => result = frame, 
                line =>
                {
                    //Debug.WriteLine(line);
                    if (duration is null && line.StartsWith(VideoTransforms.DurationOutputPrefix, StringComparison.Ordinal))
                    {
                        var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            duration = parts[1];
                    }
                    if (metadata is null && line.StartsWith(VideoTransforms.EncodingOutputPrefix, StringComparison.Ordinal))
                    {
                        var i = Math.Max(VideoTransforms.EncodingOutputPrefix.Length, 
                            line.IndexOf(": ", VideoTransforms.EncodingOutputPrefix.Length, StringComparison.Ordinal));
                        metadata = (duration is null ? null : duration + ",") + line[(i+1) ..];
                    }
                }), ct).GetAwaiter().GetResult();
            return (result ?? throw new FileFormatException(), metadata);
        }
    }
}
