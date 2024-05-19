using PhotoLocator.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace PhotoLocator.PictureFileFormats
{
    static class JpegTransformations
    {
        public static bool IsFileTypeSupported(string fileName)
        {
            return Path.GetExtension(fileName).ToUpperInvariant() is ".JPG" or ".JPEG";
        }

        public static void Rotate(string sourceFileName, string newFileName, int angleDegrees)
        {
            ProcessFile(sourceFileName, newFileName, angleDegrees.ToString(CultureInfo.InvariantCulture));
        }

        public static void Crop(string sourceFileName, string newFileName, int left, int top, int width, int height)
        {
            ProcessFile(sourceFileName, newFileName, $"{left} {top} {width} {height}");
        }

        public static void Crop(string sourceFileName, string newFileName, Rect cropRect)
        {
            Crop(sourceFileName, newFileName, IntMath.Round(cropRect.Left), IntMath.Round(cropRect.Top),
                Math.Max(1, IntMath.Round(cropRect.Width)), Math.Max(1, IntMath.Round(cropRect.Height)));
        }

        private static readonly char[] _lineSeparators = new[] { '\n', '\r' };

        private static void ProcessFile(string sourceFileName, string newFileName, string args)
        {
            var startInfo = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(typeof(JpegTransformations).Assembly.Location)!, "JpegTransform.exe"),
                $"\"{sourceFileName}\" \"{newFileName}\" {args}");
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start JpegTransform");
            var output = process.StandardOutput.ReadToEnd(); // We must read before waiting
            if (!process.WaitForExit(60000))
                throw new TimeoutException();
            if (process.ExitCode != 0)
            {
                var lines = output.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                throw new UserMessageException(lines.First());
            }
        }
    }
}
