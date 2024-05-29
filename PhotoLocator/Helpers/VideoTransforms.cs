using PhotoLocator.Settings;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    class VideoTransforms
    {
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
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var stdErrorTask = ProcessOutputAsync(process.StandardError, stdErrorCallback);
            await stdErrorTask;
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new UserMessageException($"Unable to process video. {_lastError}\nCommand line: {args}");
        }

        public async Task RunFFmpegWithStreamOutputImagesAsync(string args, Action<BitmapSource> imageCallback, Action<string> stdErrorCallback)
        {
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var processImagesTask = Task.Run(() => ProcessImages(process.StandardOutput, imageCallback));
            var stdErrorTask = ProcessOutputAsync(process.StandardError, stdErrorCallback);
            await processImagesTask;
            await stdErrorTask;
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new UserMessageException($"Unable to process video. {_lastError}\nCommand line: {args}");
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
                    var image = BitmapDecoder.Create(memStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    memStream.SetLength(0);
                    imageCallback(image.Frames[0]);
                }
            }
            catch (EndOfStreamException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async Task ProcessOutputAsync(StreamReader output, Action<string> lineCallback)
        {
            while (true)
            {
                var line = await output.ReadLineAsync();
                if (line is null)
                    return;
                _lastError = line;
                lineCallback(line);
            }
        }
    }
}
