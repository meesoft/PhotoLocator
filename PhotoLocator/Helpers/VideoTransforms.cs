using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    class VideoTransforms
    {
        public const string DurationOutputPrefix = "  Duration:";
        public const string EncodingOutputPrefix = "  Stream #0:";

        readonly ISettings _settings;
        string? _lastError;

        public VideoTransforms(ISettings settings)
        {
            _settings = settings;
        }

        private string GetFFmpegPath()
        {
            if (string.IsNullOrEmpty(_settings.FFmpegPath))
                throw new UserMessageException("FFmpeg must be installed and the path configured in Settings");
            return _settings.FFmpegPath;
        }

        public async Task RunFFmpegAsync(string args, Action<string> stdErrorCallback)
        {
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            using var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var stdErrorTask = ProcessOutputAsync(process.StandardError, stdErrorCallback);
            await stdErrorTask.ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new UserMessageException($"Unable to process video. {_lastError}\nCommand line: ffmpeg {args}");
        }

        /// <summary> Process video with streaming output to images </summary>
        /// <param name="args">Command line arguments excluding output specification</param>
        public async Task RunFFmpegWithStreamOutputImagesAsync(string args, Action<BitmapSource> imageCallback, Action<string> stdErrorCallback)
        {
            args += " -c:v bmp -f image2pipe -";
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            using var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var processImagesTask = Task.Run(() => ProcessImages(process.StandardOutput, imageCallback));
            var stdErrorTask = ProcessOutputAsync(process.StandardError, stdErrorCallback);
            await processImagesTask.ConfigureAwait(false);
            await stdErrorTask.ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new UserMessageException($"Unable to process video. {_lastError}\nCommand line: ffmpeg {args}");
        }

        private static void ProcessImages(StreamReader standardOutput, Action<BitmapSource> imageCallback)
        {
            try
            {
                var header = new byte[6];
                var theRest = Array.Empty<byte>();
                var memStream = new MemoryStream();
                while (true)
                {
                    standardOutput.BaseStream.ReadExactly(header, 0, header.Length);
                    var remainingSize = BitConverter.ToInt32(header, 2) - header.Length;
                    if (remainingSize > theRest.Length)
                        theRest = new byte[remainingSize];
                    standardOutput.BaseStream.ReadExactly(theRest, 0, remainingSize);
                    memStream.Write(header, 0, header.Length);
                    memStream.Write(theRest, 0, remainingSize);
                    var image = BitmapDecoder.Create(memStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    image.Freeze();
                    memStream.SetLength(0);
                    imageCallback(image);
                }
            }
            catch (EndOfStreamException) { }
        }

        /// <summary> Process video with streaming input from images </summary>
        /// <param name="args">Command line arguments excluding input specification</param>
        public async Task RunFFmpegWithStreamInputImagesAsync(double inFrameRate, string args, IEnumerable<BitmapSource> images, Action<string> stdErrorCallback)
        {
            var enumerator = images.GetEnumerator();
            enumerator.Reset();
            await Task.Yield();
            if (!enumerator.MoveNext())
                throw new UserMessageException("No source frames");

            var image = enumerator.Current;
            var width = image.PixelWidth;
            var height = image.PixelHeight;
            var pixelFormat = image.Format;
            string formatString;
            int pixelSize;
            if (pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32)
            {
                pixelSize = 4;
                formatString = "bgr0";
            }
            else if (pixelFormat == PixelFormats.Rgb24)
            {
                pixelSize = 3;
                formatString = "rgb24";
            }
            else if (pixelFormat == PixelFormats.Bgr24)
            {
                pixelSize = 3;
                formatString = "bgr24";
            }
            else if (pixelFormat == PixelFormats.Gray8)
            {
                pixelSize = 1;
                formatString = "gray";
            }
            else
                throw new UserMessageException("Unsupported pixel format " + pixelFormat);
            var pixels = new byte[width * height * pixelSize];

            args = string.Create(CultureInfo.InvariantCulture, $"-f rawvideo -pix_fmt {formatString} -s {width}x{height} -r {inFrameRate} -i - {args}");
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            using var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var stdErrorTask = ProcessOutputAsync(process.StandardError, stdErrorCallback);
            while (true)
            {
                image.CopyPixels(pixels, width * pixelSize, 0);
                process.StandardInput.BaseStream.Write(pixels);

                if (!enumerator.MoveNext())
                    break;
                image = enumerator.Current;
                if (pixelFormat != image.Format)
                    throw new UserMessageException("Pixel format changed");
                else if (width != image.PixelWidth || height != image.PixelHeight)
                    throw new UserMessageException("Size changed");
            }
            process.StandardInput.Close();
            await stdErrorTask.ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new UserMessageException($"Unable to process video. {_lastError}\nCommand line: ffmpeg {args}");
        }

        private async Task ProcessOutputAsync(StreamReader output, Action<string> lineCallback)
        {
            while (true)
            {
                var line = await output.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    return;
                _lastError = line;
                lineCallback(line);
            }
        }
    }
}
