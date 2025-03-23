using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    class JpegliEncoder
    {
        public static void SaveToFile(BitmapSource image, string targetPath, BitmapMetadata? metadata, int quality, string encoderPath)
        {
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(image));
            using var srcStream = new MemoryStream();
            pngEncoder.Save(srcStream);
            srcStream.Position = 0;

            using var jpgStream = new MemoryStream();
            Process(encoderPath, srcStream, "png", jpgStream, "jpg", $" -q {quality}"); // -d 0.8 --chroma_subsampling=422

            jpgStream.Position = 0;
            var finalStream = metadata is null ? jpgStream : ExifHandler.SetJpegMetadata(jpgStream, metadata);

            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            finalStream.CopyTo(fileStream);
        }

        private static void Process(string executablePath, Stream srcStream, string srcFormatExt, Stream dstStream, string dstFormatExt, string? arguments = null)
        {
            using var process = new Process();
            process.StartInfo.FileName = executablePath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            using var sourcePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            using var destPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            process.StartInfo.Arguments =
                $":{sourcePipe.GetClientHandleAsString()}.{srcFormatExt} " +
                $":{destPipe.GetClientHandleAsString()}.{dstFormatExt} {arguments}";
            process.Start();
            sourcePipe.DisposeLocalCopyOfClientHandle();
            destPipe.DisposeLocalCopyOfClientHandle();
            string? output = null;
            var outputTask = Task.Run(() => output = process.StandardError.ReadToEnd());
            try
            {
                try
                {
                    using (var sourceWriter = new BinaryWriter(sourcePipe))
                    {
                        var srcBytes = new byte[srcStream.Length];
                        srcStream.Read(srcBytes, 0, srcBytes.Length);
                        sourceWriter.Write(srcBytes.Length);
                        sourceWriter.Write(srcBytes);
                    }
                    using (var destReader = new BinaryReader(destPipe))
                    {
                        var size = destReader.ReadInt32();
                        if (size <= 0)
                            throw new IOException("jpegli encoder failed, result is empty");
                        var destBytes = destReader.ReadBytes(size);
                        if (destBytes.Length != size)
                            throw new IOException("Failed to read all bytes from destination pipe");
                        dstStream.Write(destBytes, 0, size);
                    }
                    if (!process.WaitForExit(60000))
                        throw new TimeoutException();
                    if (process.ExitCode != 0)
                        throw new IOException("jpegli failed with exit code " + process.ExitCode);
                }
                finally
                {
                    if (outputTask.Wait(1000))
                        Log.Write(output);
                }
            }
            catch (Exception ex)
            {
                if (output is null)
                    throw;
                throw new IOException("jpegli failed with: " + output, ex);
            }
        }
    }
}
