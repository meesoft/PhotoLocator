using Moq;
using PhotoLocator.Helpers;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator;

[TestClass]
public class VideoTransformCommandsTest
{
    string _testDir;
    string _ffmpegPath;

    public TestContext TestContext { get; set; }

    public VideoTransformCommandsTest()
    {
        _testDir = Path.GetDirectoryName(GetType().Assembly.Location)!;
        _ffmpegPath = Path.Combine(_testDir, VideoProcessingTest.FFmpegPath);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (!File.Exists(_ffmpegPath))
            Assert.Inconclusive("FFmpegPath not found: " + _ffmpegPath);
    }

    private Mock<IMainViewModel> SetupMainViewModelMoq(string[] sourceFiles)
    {
        var settings = new ObservableSettings() { FFmpegPath = _ffmpegPath };
        var mainViewModelMoq = new Mock<IMainViewModel>();
        mainViewModelMoq.Setup(m => m.Settings).Returns(settings);
        mainViewModelMoq.Setup(m => m.GetSelectedItems(It.IsAny<bool>())).Returns(
            sourceFiles.Select(fn => new PictureItemViewModel(Path.Combine(_testDir, fn), false, null, settings)));
        return mainViewModelMoq;
    }

    [STATestMethod]
    public void ProcessSelected_ShouldHandleAllEffects()
    {
        var mainViewModelMoq = SetupMainViewModelMoq([VideoProcessingTest.SourceVideoPath]);
        Task? processTask = null;
        mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
            {
                return processTask = body(_ => { }, CancellationToken.None);
            });

        var outputFileName = Path.Combine(_testDir, "effect.mp4");
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

    [STATestMethod]
    public void ProcessSelected_ShouldStabilize()
    {
        var mainViewModelMoq = SetupMainViewModelMoq([VideoProcessingTest.SourceVideoPath]);
        Task? processTask = null;
        mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
            {
                return processTask = body(_ => { }, CancellationToken.None);
            });

        var outputFileName = Path.Combine(_testDir, "stabilized.mp4");
        File.Delete(outputFileName);
        var commands = new VideoTransformCommands(mainViewModelMoq.Object);
        commands.IsStabilizeChecked = true;
        commands.ProcessSelected.Execute(outputFileName);
        processTask?.Wait(TestContext.CancellationToken);
        Assert.IsTrue(File.Exists(outputFileName));
    }
}
