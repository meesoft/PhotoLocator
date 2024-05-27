using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator.Helpers
{
    [TestClass]
    public class VideoTransformsTest
    {
        const string FFmpegPath = "";
        const string VideoPath = "";

        [TestMethod]
        public async Task RunFFmpegWithStreamOutputImagesAsync_ShouldProcessImages()
        {
            if (string.IsNullOrEmpty(FFmpegPath))
                Assert.Inconclusive("FFmpegPath not set");
            var settings = new ObservableSettings() { FFmpegPath = FFmpegPath };
            var videoTransforms = new VideoTransforms(settings);

            var args = $" -i \"{VideoPath}\" -c:v bmp -f image2pipe -";
            int i = 0;
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, frame =>
            {
                Debug.WriteLine(frame.PixelWidth + "x" + frame.PixelHeight);
                i++;
            }, stdError => Debug.WriteLine(stdError));
            Debug.WriteLine(i);
            Assert.IsTrue(i > 0);
        }
    }
}
