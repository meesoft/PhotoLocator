using PhotoLocator.Metadata;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    [TestClass]
    public class JpegXlFileFormatHandlerTest
    {
        const string DecoderPath = @"jpegli\djxl.exe";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SaveToFile_ShouldCreateJxl()
        {
            if (!File.Exists(JpegXlFileFormatHandler.EncoderPath))
                Assert.Inconclusive("Encoder not found in " + Path.GetFullPath(JpegXlFileFormatHandler.EncoderPath));

            const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";
            const string TargetPathJxl = @"test.jxl";

            Debug.WriteLine($"Source size: {new FileInfo(SourcePath).Length / 1024} kb");

            using var sourceFile = File.OpenRead(SourcePath);
            var source = GeneralFileFormatHandler.LoadFromStream(sourceFile, Rotation.Rotate0, int.MaxValue, true, TestContext.CancellationToken);
            sourceFile.Position = 0;
            var metadata = ExifHandler.LoadMetadata(sourceFile);

            JpegXlFileFormatHandler.SaveToFile(source, TargetPathJxl, metadata, 95);

            Assert.IsTrue(File.Exists(TargetPathJxl), "Target file was not created");
            var targetSize = new FileInfo(TargetPathJxl).Length;
            Debug.WriteLine($"Target size: {targetSize / 1024} kb");
        }

        [TestMethod]
        public void TranscodeToJxl_ShouldBeLossless()
        {
            if (!File.Exists(JpegXlFileFormatHandler.EncoderPath))
                Assert.Inconclusive("Encoder not found in " + JpegXlFileFormatHandler.EncoderPath);
            if (!File.Exists(DecoderPath))
                Assert.Inconclusive("Decoder not found in " + Path.GetFullPath(DecoderPath));

            const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";
            var targetJxl = Path.GetFileNameWithoutExtension(SourcePath) + ".jxl";
            var restoredJpg = Path.ChangeExtension(targetJxl, ".jpg");

            var sourceBytes = File.ReadAllBytes(SourcePath);
            Debug.WriteLine($"Source size: {sourceBytes.Length / 1024} kb");

            JpegXlFileFormatHandler.TranscodeToJxl(SourcePath, targetJxl, null, TestContext.CancellationToken);

            var targetSize = new FileInfo(targetJxl).Length;
            Debug.WriteLine($"Target size: {targetSize / 1024} kb");

            Process.Start(new ProcessStartInfo
            {
                FileName = DecoderPath,
                Arguments = $"\"{targetJxl}\" \"{restoredJpg}\"",
                CreateNoWindow = true,
            })?.WaitForExit();

            var restoredBytes = File.ReadAllBytes(restoredJpg);
            Assert.IsTrue(Enumerable.SequenceEqual(sourceBytes, restoredBytes), "The restored image is not identical to the original");
        }
    }
}
