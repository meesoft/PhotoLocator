using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations;

[TestClass]
public class MaxFramesOperationTest
{
    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(0.5f, 0)]
    public void GetResult8_ShouldReturnMax(float firstImageValue, float secondImageValue)
    {
        var floatImage1 = new FloatBitmap(6, 4, 3);
        floatImage1.ProcessElementWise(p => firstImageValue);
        var floatImage2 = new FloatBitmap(floatImage1.Width, floatImage1.Height, floatImage1.PlaneCount);
        floatImage2.ProcessElementWise(p => secondImageValue);

        var op = new MaxFramesOperation(null, null, default);
        var sw = Stopwatch.StartNew();
        op.ProcessImage(floatImage1.ToBitmapSource(96, 96, 1));
        op.ProcessImage(floatImage2.ToBitmapSource(96, 96, 1));
        Console.WriteLine(sw.ElapsedMilliseconds);

        Assert.IsTrue(op.IsResultReady);
        Assert.AreEqual(2, op.ProcessedImages);

        var result = new FloatBitmap(op.GetResult8(), 1);
        Assert.AreEqual(Math.Max(firstImageValue, secondImageValue), result.Mean(), 0.01);
    }

    [TestMethod]
    public void ProcessImage_ShouldUseDarkFrame()
    {
        using var bitmapStream = File.OpenRead(@"TestData\2022-06-17_19.03.02.jpg");
        var darkFrameDecoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
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
