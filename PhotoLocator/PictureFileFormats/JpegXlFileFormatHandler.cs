using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    public static class JpegXlFileFormatHandler
    {
        public const string EncoderName = "cjxl.exe";
        internal static string EncoderPath { get; }  = Path.Combine(AppContext.BaseDirectory, "jpegli", EncoderName);

        public static void SaveToStream(BitmapSource bitmap, Stream dest, string encoderPath, BitmapMetadata? metadata, int quality)
        {
            using var srcStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            if (metadata is null)
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
            else
                encoder.Frames.Add(BitmapFrame.Create(bitmap, null, ExifHandler.CreateMetadataForEncoder(metadata, encoder), null));
            encoder.Save(srcStream);
            srcStream.Position = 0;
            JpegliEncoder.Process(encoderPath, srcStream, ".png", dest, ".jxl", $"-q {quality}");
        }

        public static void SaveToFile(BitmapSource image, string targetPath, BitmapMetadata? metadata, int quality)
        {
            using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            SaveToStream(image, stream, EncoderPath, metadata, quality);
        }

        public static void TranscodeToJxl(string sourcePath, string targetPath, string? arguments, CancellationToken ct)
        {
            using var process = new Process();
            process.StartInfo.FileName = EncoderPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.Arguments = $"\"{sourcePath}\" \"{targetPath}\" {arguments}";
            process.Start();
            string? output = null;
            var outputTask = Task.Run(() => output = process.StandardError.ReadToEnd(), ct);
            try
            {
                try
                {
                    if (!process.WaitForExit(60000))
                        throw new TimeoutException();
                    if (process.ExitCode != 0)
                        throw new IOException("Codec failed with exit code " + process.ExitCode);
                }
                finally
                {
                    if (outputTask.Wait(1000, ct))
                        Log.Write(output);
                }
            }
            catch (Exception ex)
            {
                if (output is null)
                    throw;
                throw new IOException("Codec failed with: " + output, ex);
            }
        }
    }
}
