namespace PhotoLocator;

[TestClass]
public class PictureItemViewModelTest
{
    [TestMethod]
    public async Task RenameAsync_ShouldIncludeSidecarFiles()
    {
        File.Create("rename1.jpg").Dispose();
        File.Delete("rename2.jpg");
        File.Create("rename1.xmp").Dispose();
        File.Delete("rename2.xmp");
        Directory.CreateDirectory("sidecar");
        File.Create(@"sidecar\rename1.jpg.cop").Dispose();
        File.Delete(@"sidecar\rename2.jpg.cop");

        var file = new PictureItemViewModel(Path.Combine(Directory.GetCurrentDirectory(), "rename1.jpg"), false, null, null);
        await file.RenameAsync("rename2.jpg", true);

        Assert.IsTrue(File.Exists("rename2.jpg"));
        Assert.IsTrue(File.Exists("rename2.xmp"));
        Assert.IsTrue(File.Exists(@"sidecar\rename2.jpg.cop"));
    }
}
