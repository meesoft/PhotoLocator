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

        public async Task RunFFmpegAsync(string args, bool redirectStandardError = false)
        {
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardError = redirectStandardError;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var output = redirectStandardError ? await process.StandardError.ReadToEndAsync() : string.Empty; // We must read before waiting
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                if (redirectStandardError)
                    throw new UserMessageException(output);
                throw new UserMessageException("Unable to process video. Command line:\n" + args);
            }
        }       

        public async Task RunFFmpegWithStreamOutputImagesAsync(string args, Action<BitmapSource> imageCallback)
        {
            var startInfo = new ProcessStartInfo(GetFFmpegPath(), args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");

            var processImagesTask = Task.Run(() => ProcessImagesAsync(process.StandardOutput, imageCallback));
            var stdErrorTask = process.StandardError.ReadToEndAsync();
            await processImagesTask;
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new UserMessageException("Unable to process video. Command line:\n" + args + "\n" + await stdErrorTask);
        }

        private static void ProcessImagesAsync(StreamReader standardOutput, Action<BitmapSource> imageCallback)
        {
            try
            {
                while (true)
                {
                    var header = ReadBytes(standardOutput.BaseStream, 6);
                    var size = BitConverter.ToInt32(header, 2);
                    var theRest = ReadBytes(standardOutput.BaseStream, size - header.Length);
                    var memStream = new MemoryStream(size);
                    memStream.Write(header, 0, header.Length);
                    memStream.Write(theRest, 0, theRest.Length);
                    var image = BitmapDecoder.Create(memStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    imageCallback(image.Frames[0]);
                }
            }
            catch (EndOfStreamException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static byte[] ReadBytes(Stream baseStream, int size)
        {
            var buffer = new byte[size];
            baseStream.ReadExactly(buffer, 0, size);
            return buffer;
        }
    }
}
