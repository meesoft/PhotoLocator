using PhotoLocator.Settings;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    [TestClass]
    public class VideoProcessingTest
    {
        public const string FFmpegPath = @"ffmpeg\ffmpeg.exe";
        const string SourceVideoPath = @"TestData\Test.mp4";

        [TestMethod]
        public async Task RunFFmpegWithStreamOutputImagesAsync_ShouldProcessImages()
        {
            if (!File.Exists(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var args = $" -i \"{SourceVideoPath}\"";
            int i = 0;
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, frame =>
            {
                Debug.WriteLine(frame.PixelWidth + "x" + frame.PixelHeight);
                i++;
            }, stdError => Debug.WriteLine(stdError), default);

            Assert.AreEqual(15, i);
        }      

        [TestMethod]
        public async Task RunFFmpegWithStreamInputImagesAsync_ShouldEncodeImages()
        {
            if (!File.Exists(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            using var frameEnumerator = new QueueEnumerable<BitmapSource>();

            var readerArgs = $" -i \"{SourceVideoPath}\"";
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, frameEnumerator.AddItem, stdError => Debug.WriteLine("Out: " + stdError), default);

            var writerArgs = $"-pix_fmt yuv420p -y out.mp4";
            var writeTask = videoTransforms.RunFFmpegWithStreamInputImagesAsync(25, writerArgs, frameEnumerator, stdError => Debug.WriteLine("In: " + stdError), default);

            await readTask;
            frameEnumerator.Break();
            await writeTask;

            Debug.Assert(File.Exists("out.mp4"), "Output file not found");
        }
    }
}
