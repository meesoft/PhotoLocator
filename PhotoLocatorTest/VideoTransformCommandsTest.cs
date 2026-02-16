using Moq;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator;

[TestClass]
public class VideoTransformCommandsTest
{
    string _testDir;

    public TestContext TestContext { get; set; }

    public VideoTransformCommandsTest()
    {
        _testDir = Path.GetDirectoryName(GetType().Assembly.Location)!;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (!File.Exists(VideoProcessingTest.FFmpegPath))
            Assert.Inconclusive("FFmpegPath not found: " + VideoProcessingTest.FFmpegPath);
        if (!File.Exists(ExifToolTest.ExifToolPath))
            Assert.Inconclusive("ExifToolPath not found: " + ExifToolTest.ExifToolPath);
    }

    private Mock<IMainViewModel> SetupMainViewModelMoq(string[] sourceFiles)
    {
        var settings = new ObservableSettings() 
        { 
            FFmpegPath = VideoProcessingTest.FFmpegPath, 
            ExifToolPath = ExifToolTest.ExifToolPath,
        };
        var mainViewModelMoq = new Mock<IMainViewModel>();
        mainViewModelMoq.Setup(m => m.Settings).Returns(settings);
        mainViewModelMoq.Setup(m => m.GetSelectedItems(It.IsAny<bool>())).Returns(
            sourceFiles.Select(fn => new PictureItemViewModel(Path.Combine(_testDir, fn), false, null, settings)));
        return mainViewModelMoq;
    }

    [TestMethod]
    public async Task ProcessSelected_ShouldHandleAllEffects()
    {
        var mainViewModelMoq = SetupMainViewModelMoq([VideoProcessingTest.SourceVideoPath]);
        Task? processTask = null;
        mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
            {
                return processTask = body(_ => { }, TestContext.CancellationToken);
            });

        foreach (var effectItem in VideoTransformCommands.Effects)
        {
            var outputFileName = Path.Combine(_testDir, $"effect_{effectItem.Text.Split(' ')[0].ToLowerInvariant()}.mp4");

            File.Delete(outputFileName);
            Debug.WriteLine("Using effect: " + effectItem.Text);
            var commands = new VideoTransformCommands(mainViewModelMoq.Object);
            commands.SelectedEffect = effectItem;
            if (effectItem.Text == VideoTransformCommands.ZoomEffect ||
                effectItem.Text == VideoTransformCommands.Crossfade)
            {
                commands.EffectParameter = "0.1";
                commands.IsScaleChecked = true;
                commands.ScaleTo = "320:180";
            }
            commands.ProcessSelected.Execute(outputFileName);
            await (processTask ?? throw new Exception("Process task not set"));
            Directory.SetCurrentDirectory(_testDir);

            Assert.IsTrue(File.Exists(outputFileName));
        }
    }

    [TestMethod]
    public async Task ProcessSelected_ShouldStabilize()
    {
        var mainViewModelMoq = SetupMainViewModelMoq([VideoProcessingTest.SourceVideoPath]);
        Task? processTask = null;
        mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
            {
                return processTask = body(_ => { }, TestContext.CancellationToken);
            });

        var outputFileName = Path.Combine(_testDir, "stabilized.mp4");
        var transformFileName = Path.Combine(_testDir, VideoTransformCommands.TransformsFileName);
        File.Delete(outputFileName);
        var commands = new VideoTransformCommands(mainViewModelMoq.Object);
        commands.IsStabilizeChecked = true;
        commands.ProcessSelected.Execute(outputFileName);
        await(processTask ?? throw new Exception("Process task not set"));
        Directory.SetCurrentDirectory(_testDir);

        Assert.IsTrue(File.Exists(outputFileName));
        Assert.IsFalse(File.Exists(transformFileName));
    }

    [TestMethod]
    public async Task CombineFade_ShouldCombineVideos()
    {
        var mainViewModelMoq = SetupMainViewModelMoq([VideoProcessingTest.SourceVideoPath, VideoProcessingTest.SourceVideoPath, VideoProcessingTest.SourceVideoPath]);
        Task? processTask = null;
        mainViewModelMoq.Setup(m => m.RunProcessWithProgressBarAsync(It.IsAny<Func<Action<double>, CancellationToken, Task>>(), It.IsAny<string>(), It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel focusItem) =>
            {
                return processTask = body(_ => { }, TestContext.CancellationToken);
            });

        var outputFileName = Path.Combine(_testDir, "combined.mp4");
       
        File.Delete(outputFileName);
        var commands = new VideoTransformCommands(mainViewModelMoq.Object);
        commands.CombineFade.Execute(outputFileName);
        await(processTask ?? throw new Exception("Process task not set"));
        Directory.SetCurrentDirectory(_testDir);

        Assert.IsTrue(File.Exists(outputFileName));
    }
}
