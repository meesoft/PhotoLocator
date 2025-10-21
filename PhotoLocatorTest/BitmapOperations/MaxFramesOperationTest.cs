using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations;

[TestClass]
public class MaxFramesOperationTest
{
    [TestMethod]
    public void ProcessImage_ShouldUseDarkFrame()
    {   
        var darkFrameDecoder = BitmapDecoder.Create(File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg"), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        var frame = darkFrameDecoder.Frames[0];

        var darkFrame = new FloatBitmap(frame.PixelWidth, frame.PixelHeight, 3);
        for (int y = 60; y < 120; y++)
            for (int x = 60 * 3; x < 120 * 3; x++)
                darkFrame.Elements[y, x] = 0.5f;
        darkFrame.SaveToFile("DarkFrame.png");

        var op = new MaxFramesOperation("DarkFrame.png", null, default);
        var sw = Stopwatch.StartNew();
        op.ProcessImage(frame);
        var time1 = sw.ElapsedMilliseconds;
        Debug.WriteLine($"First frame processing time: {time1}ms");
        sw.Restart();
        op.ProcessImage(frame);
        var time2 = sw.ElapsedMilliseconds;
        Debug.WriteLine($"Second frame processing time: {time2}ms");

        //PictureFileFormats.GeneralFileFormatHandler.SaveToFile(op.GetResult8(), "MaxFrame.jpg");
        Assert.AreEqual(2, op.ProcessedImages);
        Assert.IsGreaterThan(time2, time1);
    }
}
