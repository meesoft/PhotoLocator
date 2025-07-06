using System.Globalization;

namespace PhotoLocator.Metadata;

[TestClass]
public class ExifToolTest
{
    const string ExifToolPath = @"exiftool\exiftool(-m).exe";

    [TestMethod]
    public async Task AdjustTimestampAsync_ShouldUpdateTimestamp()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        const string TargetFileName = @"TestData\2022-06-17_18.03.02.jpg";

        await ExifTool.AdjustTimeStampAsync(@"TestData\2022-06-17_19.03.02.jpg", TargetFileName, "-01:00:00", ExifToolPath, default);

        using var targetFile = File.OpenRead(TargetFileName);
        var metadata = ExifHandler.LoadMetadata(targetFile);
        var tag = ExifHandler.DecodeTimeStamp(metadata!, targetFile) ?? throw new FileFormatException("Failed to decode timestamp");
        Assert.AreEqual(new DateTime(2022, 06, 17, 18, 03, 02, DateTimeKind.Local), tag);
    }

    [TestMethod]
    public async Task TransferMetadataAsync_ShouldCopyMetadataToTargetFile()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        const string MetadataFile = @"TestData\2022-06-17_19.03.02.jpg";
        const string SourceFile = @"TestData\2025-05-04_15.13.08-04.jpg";
        const string TargetFile = @"TestData\2025-05-04_15.13.08-04-metadata.jpg";
        File.Delete(TargetFile);

        await ExifTool.TransferMetadataAsync(MetadataFile, SourceFile, TargetFile, ExifToolPath, CancellationToken.None);

        var sourceMetadata = ExifTool.LoadMetadata(MetadataFile, ExifToolPath);
        var targetMetadata = ExifTool.LoadMetadata(TargetFile, ExifToolPath);
        Assert.AreEqual(sourceMetadata["Model"], targetMetadata["Model"]);
        Assert.AreEqual(sourceMetadata["DateTimeOriginal"], targetMetadata["DateTimeOriginal"]);
        Assert.AreEqual(sourceMetadata["ISO"], targetMetadata["ISO"]);
    }

    [TestMethod]
    public async Task TransferMetadataAsync_ShouldWorkInPlace()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        const string MetadataFile = @"TestData\2022-06-17_19.03.02.jpg";
        const string SourceFile = @"TestData\2025-05-04_15.13.08-04.jpg";
        const string TempFile = @"TestData\2025-05-04_15.13.08-04-inplace.jpg";
        File.Copy(SourceFile, TempFile, true);

        await ExifTool.TransferMetadataAsync(MetadataFile, TempFile, TempFile, ExifToolPath, CancellationToken.None);

        var sourceMetadata = ExifTool.LoadMetadata(MetadataFile, ExifToolPath);
        var targetMetadata = ExifTool.LoadMetadata(TempFile, ExifToolPath);
        Assert.AreEqual(sourceMetadata["Model"], targetMetadata["Model"]);
        Assert.AreEqual(sourceMetadata["DateTimeOriginal"], targetMetadata["DateTimeOriginal"]);
        Assert.AreEqual(sourceMetadata["ISO"], targetMetadata["ISO"]);
    }

    [TestMethod]
    public async Task SetGeotag_ShouldSet_UsingBitmapMetadata()
    {
        var setValue = new MapControl.Location(-10, -20);
        await ExifTool.SetGeotagAsync(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out1.jpg", setValue, null, default);

        var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out1.jpg");
        Assert.AreEqual(setValue, newValue);
    }

    [TestMethod]
    public async Task SetGeotag_ShouldSet_UsingExifTool()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        var setValue = new MapControl.Location(-10, -20);
        await ExifTool.SetGeotagAsync(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02-out2.jpg", setValue, ExifToolPath, default);

        var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02-out2.jpg");
        Assert.AreEqual(setValue, newValue);
    }

    [TestMethod]
    public async Task SetGeotag_ShouldSet_UsingExifTool_InPlace()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        var setValue = new MapControl.Location(-10, -20);
        File.Copy(@"TestData\2022-06-17_19.03.02.jpg", @"TestData\2022-06-17_19.03.02_copy.jpg", true);
        await ExifTool.SetGeotagAsync(@"TestData\2022-06-17_19.03.02_copy.jpg", @"TestData\2022-06-17_19.03.02_copy.jpg", setValue, ExifToolPath, default);

        var newValue = ExifHandler.GetGeotag(@"TestData\2022-06-17_19.03.02_copy.jpg");
        Assert.AreEqual(setValue, newValue);
    }

    [TestMethod]
    public async Task SetGeotag_ShouldSetInCr3_UsingExifTool()
    {
        const string FileName = @"TestData\Test.CR3";

        if (!File.Exists(FileName))
            Assert.Inconclusive("Image not found");
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        var setValue = new MapControl.Location(-10, -20);
        await ExifTool.SetGeotagAsync(FileName, "tagged.cr3", setValue, ExifToolPath, default);

        var newValue = ExifHandler.GetGeotag("tagged.cr3");
        Assert.AreEqual(setValue, newValue);
    }

    [TestMethod]
    public void DecodeMetadata_ShouldDecode()
    {
        if (!File.Exists(ExifToolPath))
            Assert.Inconclusive("ExifTool not found");

        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var metadata = ExifTool.DecodeMetadata(@"TestData\2022-06-17_19.03.02.jpg", ExifToolPath);

        Assert.AreEqual("FC7303, 1/80s, f/2.8, 4.5 mm, ISO100, 341x191, " + ExifHandlerTest.JpegTestDataTimestamp, metadata.Metadata);
        Assert.AreEqual(new DateTimeOffset(2022, 6, 17, 19, 3, 2, TimeSpan.FromHours(2)), metadata.TimeStamp);
        Assert.AreEqual(55.4, metadata.Location!.Latitude, 0.1);
        Assert.AreEqual(11.2, metadata.Location!.Longitude, 0.1);
    }

    [TestMethod]
    public void DecodeTimestamp_ShouldUseOffset()
    {
        var dict = new Dictionary<string, string>
            {
                { "DateTimeOriginal", "2025:02:01 22:14:54" },
                { "OffsetTimeOriginal", "+01:00" }
            };

        var timestamp = ExifTool.DecodeTimeStamp(dict);

        Assert.AreEqual(new DateTimeOffset(2025, 2, 1, 22, 14, 54, TimeSpan.FromHours(1)), timestamp);
    }

    [TestMethod]
    [DataRow(@"TestData\Canon90DVideo.txt", 2024, 7, 9, 14, 38, 53, 0, +2)]
    [DataRow(@"TestData\DJIAction2Video.txt", 2022, 4, 16, 18, 46, 28, 0, +2)]
    [DataRow(@"TestData\iPhoneVideo.txt", 2022, 9, 23, 12, 50, 53, 0, +2)]
    [DataRow(@"TestData\Mini2Video.txt", 2024, 7, 9, 13, 9, 22, 0, +2)]
    [DataRow(@"TestData\Pixel5Video.txt", 2025, 4, 26, 17, 6, 45, 0, +2)]
    public void DecodeTimestamp_ShouldHandleDifferentFormats(string fileName, int year, int month, int day, int hour, int minutes, int seconds, int ms, int offset)
    {
        var metadata = ExifTool.DecodeMetadataToDictionary(File.ReadAllLines(fileName));

        var decoded = ExifTool.DecodeTimeStamp(metadata);

        Assert.AreEqual(new DateTimeOffset(year, month, day, hour, minutes, seconds, ms, TimeSpan.FromHours(offset)), decoded);
    }
}
