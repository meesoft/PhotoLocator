using PhotoLocator.Metadata;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    [TestClass]
    public class JpegliEncoderTest
    {
        const string EncoderPath = @"..\..\..\..\PhotoLocator\bin\Debug\net8.0-windows\cjpegli.exe";

        [TestMethod]
        public void JpegliEncode()
        {
            if (!File.Exists(EncoderPath))
                Assert.Inconclusive("Encoder not found in "+Path.GetFullPath(EncoderPath));

            const string SourcePath = @"TestData\2022-06-17_19.03.02.jpg";
            const string TargetPathJpeg = @"jpeg.jpg";
            const string TargetPathJpegli = @"jpegli.jpg";

            Console.WriteLine($"Source size: {new FileInfo(SourcePath).Length / 1024} kb");

            using var sourceFile = File.OpenRead(SourcePath);
            var source = GeneralFileFormatHandler.LoadFromStream(sourceFile, Rotation.Rotate0, int.MaxValue, true, default);
            sourceFile.Position = 0;
            var metadata = ExifHandler.LoadMetadata(sourceFile);

            GeneralFileFormatHandler.SaveToFile(source, TargetPathJpeg, metadata, 95);
            var sizeJpeg = new FileInfo(TargetPathJpeg).Length;
            Console.WriteLine($"Dest size jpeg: {sizeJpeg / 1024} kb");

            JpegliEncoder.SaveToFile(source, TargetPathJpegli, metadata, 94, EncoderPath);
            var sizeJpegli = new FileInfo(TargetPathJpegli).Length;
            Console.WriteLine($"Dest size jpegli: {sizeJpegli / 1024} kb");
            Console.WriteLine($"{100.0 * sizeJpegli / sizeJpeg:0.0}%");
        }
    }
}
