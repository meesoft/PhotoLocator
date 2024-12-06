using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    class JpegliEncoder
    {
        public static void SaveToFile(BitmapSource image, string targetPath, BitmapMetadata? metadata, int Quality, string encoderPath)
        {
            var tempPngPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(targetPath) + ".png");
            var encodedJpegPath = metadata is null ? targetPath : Path.Combine(Path.GetTempPath(), Path.GetFileName(targetPath));
            GeneralFileFormatHandler.SaveToFile(image, tempPngPath);
            try
            {
                var process = Process.Start(new ProcessStartInfo(encoderPath, $"\"{tempPngPath}\" \"{encodedJpegPath}\" -q {Quality}")
                {
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                }) ?? throw new IOException("Failed to start " + encoderPath);
                var output = process.StandardError.ReadToEnd(); // We must read before waiting
                if (!process.WaitForExit(60000))
                    throw new TimeoutException();
                if (process.ExitCode != 0)
                    throw new UserMessageException(output);
                if (metadata is not null)
                    ExifHandler.SetMetadata(encodedJpegPath, targetPath, metadata);
            }
            finally
            {
                File.Delete(tempPngPath);
                if (encodedJpegPath != targetPath)
                    File.Delete(encodedJpegPath);
            }
        }
    }
}
