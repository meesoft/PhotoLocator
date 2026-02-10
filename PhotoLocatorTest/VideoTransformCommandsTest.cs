using Moq;
using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator
{
    [TestClass]
    public class VideoTransformCommandsTest
    {
        public TestContext TestContext { get; set; }

        [STATestMethod]
        public void ProcessSelected_ShouldHandleAllEffects()
        {
            var testDir = Directory.GetCurrentDirectory();
            var ffmpegPath = Path.Combine(testDir, VideoProcessingTest.FFmpegPath);
            if (!File.Exists(ffmpegPath))
                Assert.Inconclusive("FFmpegPath not found: " + ffmpegPath);
            if (!File.Exists(VideoProcessingTest.SourceVideoPath))
                Assert.Inconclusive("Source video not found: " + VideoProcessingTest.SourceVideoPath);

            Task? processTask = null;
            var settings = new ObservableSettings() { FFmpegPath = ffmpegPath };
            var mainViewModelMoq = new Mock<IMainViewModel>();
            mainViewModelMoq.Setup(m => m.Settings).Returns(settings);
            mainViewModelMoq.Setup(m => m.GetSelectedItems(It.IsAny<bool>())).Returns(
                [new PictureItemViewModel(Path.Combine(testDir, VideoProcessingTest.SourceVideoPath), false, null, settings)]);
            mainViewModelMoq.Setup(m => m.PauseFileSystemWatcher()).Returns((IAsyncDisposable)null!);
            mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
                .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
                {
                    return processTask = body(_ => { }, CancellationToken.None);
                });

            var outputFileName = Path.Combine(testDir, "effect.mp4");
            foreach (var effectItem in VideoTransformCommands.Effects)
            {
                File.Delete(outputFileName);
                Debug.WriteLine("Using effect: " + effectItem.Content);
                var commands = new VideoTransformCommands(mainViewModelMoq.Object);
                commands.SelectedEffect = effectItem;
                commands.ProcessSelected.Execute(outputFileName);
                processTask?.Wait(TestContext.CancellationToken);

                Assert.IsTrue(File.Exists(outputFileName));
            }
        }
    }
}
