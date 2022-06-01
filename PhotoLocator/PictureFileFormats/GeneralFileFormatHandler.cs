using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class GeneralFileFormatHandler
    {
        public static BitmapSource? TryLoadFromStream(Stream source, Rotation rotation, int maxWidth, CancellationToken ct)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = source;
            if (maxWidth < int.MaxValue)
                bitmap.DecodePixelWidth = maxWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.Rotation = rotation;
            bitmap.EndInit();
            ct.ThrowIfCancellationRequested();
            bitmap.Freeze();
            ct.ThrowIfCancellationRequested();
            return bitmap;
        }
    }
}
