using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class StarfieldRendererTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task GenerateFrames_ShouldGenerateVideo()
        {
            const string TargetPath = @"Starfield.mp4";
            const double FrameRate = 30;
            const double Duration = 10;

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");

            var renderers = new StarfieldRenderer(3840, 2160, 10000, speed: 0.001f, growthFactor: 2f, seed: 42);

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var writerArgs = $"-pix_fmt yuv420p -y {TargetPath}";
            await videoTransforms.RunFFmpegWithStreamInputImagesAsync(FrameRate, writerArgs, renderers.GenerateFrames((int)(FrameRate * Duration)), 
                stdError => Debug.WriteLine(stdError), TestContext.CancellationToken);
        }
    }
}
