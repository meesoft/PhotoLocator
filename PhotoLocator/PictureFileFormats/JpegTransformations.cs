using PhotoLocator.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PhotoLocator.PictureFileFormats
{
    static class JpegTransformations
    {
        public static bool IsFileTypeSupported(string fileName)
        {
            return Path.GetExtension(fileName).ToUpperInvariant() is ".JPG" or ".JPEG";
        }

        public static void Rotate(string fileName, int angleDegrees)
        {
            ProcessFile(fileName, angleDegrees.ToString(CultureInfo.InvariantCulture));
        }

        public static void Crop(string fileName, int left, int top, int width, int height)
        {
            ProcessFile(fileName, $"{left} {top} {width} {height}");
        }

        private static readonly char[] _lineSeparators = new[] { '\n', '\r' };

        private static void ProcessFile(string fileName, string args)
        {
            var startInfo = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(typeof(JpegTransformations).Assembly.Location)!, "JpegTransform.exe"),
                $"\"{fileName}\" \"{fileName}\" {args}");
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start JpegTransform");
            if (!process.WaitForExit(60000))
                throw new TimeoutException();
            //if (process.ExitCode != 0)
            var output = process.StandardOutput.ReadToEnd() + '\n' + process.StandardError.ReadToEnd();
            var lines = output.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
                throw new UserMessageException(lines.Last());
        }
    }
}
