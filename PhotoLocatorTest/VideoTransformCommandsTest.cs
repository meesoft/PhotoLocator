using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

namespace PhotoLocator
{
    [TestClass]
    public class VideoTransformCommandsTest
    {
        [STATestMethod]
        public async Task ProcessVideo_WithEachEffect()
        {
            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (!File.Exists(VideoProcessingTest.SourceVideoPath))
                Assert.Inconclusive("Source video not found");

            Task executeTask = null;

            var testDir = Directory.GetCurrentDirectory();
            var outputFileName = Path.Combine(testDir, "out.mp4");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var mainViewModelMoq = new Mock<IMainViewModel>();
            mainViewModelMoq.Setup(m => m.Settings).Returns(settings);
            mainViewModelMoq.Setup(m => m.GetSelectedItems(It.IsAny<bool>())).Returns(
                [new PictureItemViewModel(Path.Combine(testDir, VideoProcessingTest.SourceVideoPath), false, null, settings)]);
            mainViewModelMoq.Setup(m => m.PauseFileSystemWatcher()).Returns((IAsyncDisposable)null!);
            mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
                .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
                {
                    return executeTask = body(_ => { }, CancellationToken.None);
                });
            mainViewModelMoq.Setup(m => m.AddOrUpdateItemAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns((string path, bool isDir, bool select) => Task.CompletedTask);

            foreach (var effectItem in VideoTransformCommands.Effects)
            {
                File.Delete(outputFileName);

                //Debug.WriteLine(effectItem.Content);
                var commands = new VideoTransformCommands(mainViewModelMoq.Object);
                commands.SelectedEffect = effectItem;
                commands.ProcessSelected.Execute(outputFileName);
                await (executeTask ?? throw new Exception("Task not started")).ConfigureAwait(true);

                Assert.IsTrue(File.Exists(outputFileName));
                File.Delete(outputFileName);
            }
        }
    }
}
