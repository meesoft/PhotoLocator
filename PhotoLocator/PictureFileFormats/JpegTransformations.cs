using PhotoLocator.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PhotoLocator.PictureFileFormats
{
    static class JpegTransformations
    {
        public static bool IsFileTypeSupported(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() is ".jpg" or ".jpeg";
        }

        public static async Task RotateAsync(string sourceFileName, string newFileName, int angleDegrees, CancellationToken ct)
        {
            await ProcessFileAsync(sourceFileName, newFileName, angleDegrees.ToString(CultureInfo.InvariantCulture), ct);
        }

        public static async Task CropAsync(string sourceFileName, string newFileName, int left, int top, int width, int height, CancellationToken ct)
        {
            await ProcessFileAsync(sourceFileName, newFileName, $"{left} {top} {width} {height}", ct);
        }

        public static async Task CropAsync(string sourceFileName, string newFileName, Rect cropRect, CancellationToken ct)
        {
            await CropAsync(sourceFileName, newFileName, IntMath.Round(cropRect.Left), IntMath.Round(cropRect.Top),
                Math.Max(1, IntMath.Round(cropRect.Width)), Math.Max(1, IntMath.Round(cropRect.Height)), ct);
        }

        private static readonly char[] _lineSeparators = ['\n', '\r'];

        private static async Task ProcessFileAsync(string sourceFileName, string newFileName, string args, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "JpegTransform.exe"),
                $"\"{sourceFileName}\" \"{newFileName}\" {args}");
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            Log.Write($"{startInfo.FileName} {startInfo.Arguments}");
            for (int i = 0; ; i++)
            {
                using var process = Process.Start(startInfo) ?? throw new IOException("Failed to start JpegTransform");
                var output = await process.StandardOutput.ReadToEndAsync(ct); // We must read before waiting
                await process.WaitForExitAsync(ct);
                if (process.ExitCode != 0)
                {
                    var lines = output.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var error = lines.First();
                    if (i < 5 && error.Contains("The process cannot access the file because it is being used by another process", StringComparison.Ordinal))
                    {
                        Log.Write($"Retrying because of file access error: {error}"); // Often happens because of Dropbox or OneDrive syncing
                        await Task.Delay(600, ct);
                        continue;
                    }
                    throw new UserMessageException(error);
                }
                break;
            }
        }
    }
}
