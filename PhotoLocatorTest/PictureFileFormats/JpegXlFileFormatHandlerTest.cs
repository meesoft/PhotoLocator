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

            File.Delete(TargetPathJxl);

            Debug.WriteLine($"Source size: {new FileInfo(SourcePath).Length / 1024} kb");

            using var sourceFile = File.OpenRead(SourcePath);
            var source = GeneralFileFormatHandler.LoadFromStream(sourceFile, Rotation.Rotate0, int.MaxValue, true, TestContext.CancellationToken);

            JpegXlFileFormatHandler.SaveToFile(source, TargetPathJxl);

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
            File.Delete(targetJxl);
            File.Delete(restoredJpg);

            var sourceBytes = File.ReadAllBytes(SourcePath);
            Debug.WriteLine($"Source size: {sourceBytes.Length / 1024} kb");

            JpegXlFileFormatHandler.TranscodeToJxl(SourcePath, targetJxl, null, TestContext.CancellationToken);

            var targetSize = new FileInfo(targetJxl).Length;
            Debug.WriteLine($"Target size: {targetSize / 1024} kb");

            var decoder = Process.Start(new ProcessStartInfo
            {
                FileName = DecoderPath,
                Arguments = $"\"{targetJxl}\" \"{restoredJpg}\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("Failed to start decoder process");
            Debug.WriteLine(decoder.StandardError.ReadToEnd());
            decoder.WaitForExit();

            var restoredBytes = File.ReadAllBytes(restoredJpg);
            Assert.IsTrue(Enumerable.SequenceEqual(sourceBytes, restoredBytes), "The restored image is not identical to the original");
        }
    }
}
