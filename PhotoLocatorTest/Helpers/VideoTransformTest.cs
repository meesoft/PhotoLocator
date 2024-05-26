using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    [TestClass]
    public class VideoTransformTest
    {
        const string FFmpegPath = "C:\\Users\\mv\\Downloads\\ffmpeg-7.0-essentials_build\\bin\\ffmpeg.exe";

        const string VideoPath = "C:\\Dropbox\\Foto\\2024 Natur og Fugle\\M\\2024-05-12 15.08.35 Korsør Lystskov [0488].MP4";

        [TestMethod]
        public async Task ShouldExtractImages()
        {
            if (string.IsNullOrEmpty(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not set");

            var args = $" -i \"{VideoPath}\" -c:v bmp -f image2pipe -";
            var startInfo = new ProcessStartInfo(FFmpegPath, args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");

            var processTask = Task.Run(() => ProcessImagesAsync(process.StandardOutput));
            var stdError = process.StandardError.ReadToEndAsync(); // We must read before waiting
            await Task.WhenAll(processTask, stdError, process.WaitForExitAsync());
            if (process.ExitCode != 0)
                throw new UserMessageException("Unable to process video. Command line:\n" + args + "\n" + await stdError);
        }

        private void ProcessImagesAsync(StreamReader standardOutput)
        {
            int i = 0;
            try
            {
                while (true)
                {
                    var head = ReadBytes(standardOutput.BaseStream, 6);
                    var size = BitConverter.ToInt32(head, 2);
                    var rest = ReadBytes(standardOutput.BaseStream, size - 6);
                    var memStream = new MemoryStream(size);
                    memStream.Write(head, 0, 6);
                    memStream.Write(rest, 0, size - 6);
                    var image = BitmapDecoder.Create(memStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    var frame = image.Frames[0];
                    Debug.WriteLine(frame.PixelWidth + "x" + frame.PixelHeight);
                    i++;
                }
            }
            catch(EndOfStreamException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            Debug.WriteLine(i);
        }

        private byte[] ReadBytes(Stream baseStream, int size)
        {
            var buffer = new byte[size];
            baseStream.ReadExactly(buffer, 0, size);
            return buffer;
        }
    }
}
