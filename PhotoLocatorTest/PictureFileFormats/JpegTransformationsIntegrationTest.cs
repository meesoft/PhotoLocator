using System.Windows;

namespace PhotoLocator.PictureFileFormats
{
    [TestClass]
    public class JpegTransformationsIntegrationTest
    {
        const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";

        [TestMethod]
        public void Rotate_ShouldProduceOutputFile()
        {
            const string Target = "rotated_test.jpg";
            File.Delete(Target);

            JpegTransformations.Rotate(SourcePath, Target, 90);

            Assert.IsTrue(File.Exists(Target), "Target file was not created");
        }

        [TestMethod]
        public void Crop_WithRect_ShouldProduceOutputFile()
        {
            const string Target = "cropped_test.jpg";
            File.Delete(Target);

            var rect = new Rect(10, 10, 100, 80);
            JpegTransformations.Crop(SourcePath, Target, rect);

            Assert.IsTrue(File.Exists(Target), "Target file was not created");
        }
    }
}
