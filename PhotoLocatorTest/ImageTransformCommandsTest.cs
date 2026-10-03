using Moq;
using PhotoLocator.Settings;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator;

[TestClass]
public class ImageTransformCommandsTest
{
    Task? _processTask = null;

    [TestMethod]
    public async Task RotateSelectedAsync_ShouldProduce48BppFile()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", "RGB48.tif");

        var settings = new ObservableSettings { SavedFilePostfix = "_rotated" };
        var item = new PictureItemViewModel(sourcePath, false, null, settings);
        await item.LoadThumbnailAndMetadataAsync(default);
        var mainViewModel = SetupMainViewModelMock(settings, item);
        var outputPath = item.GetProcessedFileName();
        File.Delete(outputPath);
        
        var commands = new ImageTransformCommands(mainViewModel.Object);
        commands.RotateRightCommand.Execute(null);
        await (_processTask ?? throw new InvalidOperationException("The rotate process was not started"));

        using var stream = File.OpenRead(outputPath);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.AreEqual(48, frame.PixelWidth);
        Assert.AreEqual(85, frame.PixelHeight);
        Assert.AreEqual(PixelFormats.Rgb48, frame.Format);
    }

    [TestMethod]
    public async Task CropSelectedItemAsync_ShouldProduce48BppFile()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", "RGB48.tif");

        var settings = new ObservableSettings { SavedFilePostfix = "_cropped" };
        var item = new PictureItemViewModel(sourcePath, false, null, settings);
        var sourceImage = await item.LoadPreviewAsync(default) ?? throw new InvalidOperationException("Unable to load source image");
        var mainViewModel = SetupMainViewModelMock(settings, item);
        var outputPath = item.GetProcessedFileName();
        File.Delete(outputPath);

        var commands = new ImageTransformCommands(mainViewModel.Object);
        await commands.CropSelectedItemAsync(sourceImage, new Rect(4, 4, 24, 20));

        using var stream = File.OpenRead(outputPath);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.AreEqual(24, frame.PixelWidth);
        Assert.AreEqual(20, frame.PixelHeight);
        Assert.AreEqual(PixelFormats.Rgb48, frame.Format);
    }

    [TestMethod]
    public async Task ResizeCommand_ShouldProduce48BppFile()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", "RGB48.tif");

        var settings = new ObservableSettings { SavedFilePostfix = "_resized" };
        var item = new PictureItemViewModel(sourcePath, false, null, settings);
        var mainViewModel = SetupMainViewModelMock(settings, item);
        var outputPath = item.GetProcessedFileName();
        File.Delete(outputPath);
        
        var commands = new ImageTransformCommands(mainViewModel.Object);
        commands.ResizeCommand.Execute((Path.GetDirectoryName(sourcePath), 24));
        await (_processTask ?? throw new InvalidOperationException("The resize process was not started"));

        using var stream = File.OpenRead(outputPath);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.AreEqual(42, frame.PixelWidth);
        Assert.AreEqual(24, frame.PixelHeight);
        Assert.AreEqual(PixelFormats.Rgb48, frame.Format);
    }

    private Mock<IMainViewModel> SetupMainViewModelMock(ObservableSettings settings, PictureItemViewModel item)
    {
        var mainViewModel = new Mock<IMainViewModel>();
        mainViewModel.Setup(m => m.Settings).Returns(settings);
        mainViewModel.Setup(m => m.SelectedItem).Returns(item);
        mainViewModel.Setup(m => m.GetSelectedItems(true)).Returns([item]);
        mainViewModel.Setup(m => m.RunProcessWithProgressBarAsync(
                It.IsAny<Func<Action<double>, CancellationToken, Task>>(),
                It.IsAny<string>(),
                It.IsAny<PictureItemViewModel>()))
            .Returns((Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel? focusItem) =>
            {
                return _processTask = body(_ => { }, CancellationToken.None);
            });
        return mainViewModel;
    }
}
