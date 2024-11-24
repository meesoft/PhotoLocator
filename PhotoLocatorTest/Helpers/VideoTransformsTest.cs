using PhotoLocator.Settings;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Helpers
{
    [TestClass]
    public class VideoTransformsTest
    {
        const string FFmpegPath = @"";
        const string SourceVideoPath = @"";

        [TestMethod]
        public async Task RunFFmpegWithStreamOutputImagesAsync_ShouldProcessImages()
        {
            if (string.IsNullOrEmpty(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not set");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoTransforms(settings);

            var args = $" -i \"{SourceVideoPath}\"";
            int i = 0;
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, frame =>
            {
                Debug.WriteLine(frame.PixelWidth + "x" + frame.PixelHeight);
                i++;
            }, stdError => Debug.WriteLine(stdError), default);
            Debug.WriteLine(i);
            Assert.IsTrue(i > 0);
        }      

        [TestMethod]
        public async Task RunFFmpegWithStreamInputImagesAsync_ShouldEncodeImages()
        {
            if (string.IsNullOrEmpty(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not set");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoTransforms(settings);

            using var frameEnumerator = new QueueEnumerable<BitmapSource>();

            var readerArgs = $" -i \"{SourceVideoPath}\"";
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, frameEnumerator.AddItem, stdError => { }, default); // Debug.WriteLine(stdError));

            var writerArgs = $"-pix_fmt yuv420p -y out.mp4";
            var writeTask = videoTransforms.RunFFmpegWithStreamInputImagesAsync(25, writerArgs, frameEnumerator, stdError => Debug.WriteLine(stdError), default);

            await readTask;
            frameEnumerator.Break();
            await writeTask;
        }

        [TestMethod]
        public async Task RunFFmpegWithStreamInputImagesAsync_ShouldProcessImages()
        {
            if (string.IsNullOrEmpty(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not set");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoTransforms(settings);

            using var frameEnumerator = new QueueEnumerable<BitmapSource>();

            var readerArgs = $" -i \"{SourceVideoPath}\"";
            var op = new LocalContrastViewModel();
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, 
                source => frameEnumerator.AddItem(op.ApplyOperations(source)),
                stdError => { }, default); 

            var writerArgs = $"-pix_fmt yuv420p -y out.mp4";
            var writeTask = videoTransforms.RunFFmpegWithStreamInputImagesAsync(25, writerArgs, frameEnumerator, stdError => Debug.WriteLine(stdError), default);

            await readTask;
            frameEnumerator.Break();
            await writeTask;
        }
    }
}
