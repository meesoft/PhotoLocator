using System.Windows;

namespace PhotoLocator.PictureFileFormats
{
    [TestClass]
    public class JpegTransformationsIntegrationTest
    {
        const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Rotate_ShouldProduceOutputFile()
        {
            const string Target = "rotated_test.jpg";
            File.Delete(Target);

            await JpegTransformations.RotateAsync(SourcePath, Target, 90, TestContext.CancellationToken);

            Assert.IsTrue(File.Exists(Target), "Target file was not created");
        }

        [TestMethod]
        public async Task Crop_WithRect_ShouldProduceOutputFile()
        {
            const string Target = "cropped_test.jpg";
            File.Delete(Target);

            var rect = new Rect(10, 10, 100, 80);
            await JpegTransformations.CropAsync(SourcePath, Target, rect, TestContext.CancellationToken);

            Assert.IsTrue(File.Exists(Target), "Target file was not created");
        }
    }
}
