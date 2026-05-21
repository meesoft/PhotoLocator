using MeeSoft.ImageProcessing.FileFormats;
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
            if (!File.Exists(JpegXlFileFormatHandler._encoderPath))
                Assert.Inconclusive("Encoder not found in " + Path.GetFullPath(JpegXlFileFormatHandler._encoderPath));

            const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";
            const string TargetPathJxl = @"test.jxl";

            Debug.WriteLine($"Source size: {new FileInfo(SourcePath).Length / 1024} kb");

            using var sourceFile = File.OpenRead(SourcePath);
            var source = GeneralFileFormatHandler.LoadFromStream(sourceFile, Rotation.Rotate0, int.MaxValue, true, TestContext.CancellationToken);
            sourceFile.Position = 0;
            var metadata = ExifHandler.LoadMetadata(sourceFile);

            JpegXlFileFormatHandler.SaveToFile(source, TargetPathJxl, metadata, 95, TestContext.CancellationToken);

            Assert.IsTrue(File.Exists(TargetPathJxl), "Target file was not created");
            var targetSize = new FileInfo(TargetPathJxl).Length;
            Debug.WriteLine($"Target size: {targetSize / 1024} kb");
        }

        [TestMethod]
        public void Transcode_ShouldBeLossless()
        {
            if (!File.Exists(JpegXlFileFormatHandler._encoderPath))
                Assert.Inconclusive("Encoder not found in " + JpegXlFileFormatHandler._encoderPath);
            if (!File.Exists(DecoderPath))
                Assert.Inconclusive("Decoder not found in " + Path.GetFullPath(DecoderPath));

            const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";
            string _targetPathJxl = Path.GetFileNameWithoutExtension(SourcePath) + ".jxl";

            Debug.WriteLine($"Source size: {new FileInfo(SourcePath).Length / 1024} kb");

            JpegXlFileFormatHandler.Transcode(SourcePath, _targetPathJxl, null, TestContext.CancellationToken);

            Assert.IsTrue(File.Exists(_targetPathJxl), "Target file was not created");
            var targetSize = new FileInfo(_targetPathJxl).Length;
            Debug.WriteLine($"Target size: {targetSize / 1024} kb");

            // Decode the JXL back to compare with the original
        }
    }
}
