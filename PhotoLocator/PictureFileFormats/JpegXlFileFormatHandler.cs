using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MeeSoft.ImageProcessing.FileFormats
{
    public static class JpegXlFileFormatHandler
    {
        public const string EncoderName = "cjxl.exe";

        internal static string _encoderPath = Path.Combine(AppContext.BaseDirectory, "jpegli", EncoderName);

        public static BitmapSource LoadFromStream(Stream source, Rotation rotation, int maxWidth, bool preservePixelFormat, string decoderPath, CancellationToken ct)
        {
            using var dstStream = new MemoryStream();
            Process(decoderPath, source, ".jxl", dstStream, ".png", ct);
            dstStream.Position = 0;
            return GeneralFileFormatHandler.LoadFromStream(new OffsetStreamReader(dstStream), rotation, maxWidth, preservePixelFormat, ct);
        }

        public static void SaveToStream(Stream dest, BitmapSource bitmap, string encoderPath, int quality, CancellationToken ct)
        {
            using var srcStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(srcStream);
            srcStream.Position = 0;
            Process(encoderPath, srcStream, ".png", dest, ".jxl", ct, $"-q {quality}");
        }

        public static void SaveToFile(BitmapSource image, string targetPath, BitmapMetadata? metadata, int quality, CancellationToken ct)
        {
            using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            SaveToStream(stream, image, _encoderPath, quality, ct);
        }

        public static void Transcode(string sourcePath, string targetPath, string? arguments, CancellationToken ct)
        {
            using var process = new Process();
            process.StartInfo.FileName = _encoderPath;
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

        private static void Process(string executablePath, Stream srcStream, string srcFormatExt, Stream dstStream, string dstFormatExt,
            CancellationToken ct, string? arguments = null)
        {
            using var process = new Process();
            process.StartInfo.FileName = executablePath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            using var sourcePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            using var destPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            process.StartInfo.Arguments =
                $":{sourcePipe.GetClientHandleAsString()}{srcFormatExt} " +
                $":{destPipe.GetClientHandleAsString()}{dstFormatExt} {arguments}";
            process.Start();
            sourcePipe.DisposeLocalCopyOfClientHandle();
            destPipe.DisposeLocalCopyOfClientHandle();
            try
            {
                using (var sourceWriter = new BinaryWriter(sourcePipe))
                {
                    var srcBytes = new byte[srcStream.Length];
                    srcStream.ReadExactly(srcBytes);
                    sourceWriter.Write(srcBytes.Length);
                    sourceWriter.Write(srcBytes);
                }
                using (var destReader = new BinaryReader(destPipe))
                {
                    var size = destReader.ReadInt32();
                    if (size == 0)
                        throw new IOException("JXL codec failed, result is empty");
                    var destBytes = destReader.ReadBytes(size);
                    dstStream.Write(destBytes, 0, size);
                }
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message + '\n' + process.StandardOutput.ReadToEnd() + '\n' + process.StandardError.ReadToEnd(), ex);
            }
        }
    }
}
