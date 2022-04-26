using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class GeneralFileFormatHandler
    {
        public static BitmapSource? TryLoadFromStream(Stream source, Rotation rotation, int maxWidth)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = source;
            if (maxWidth < int.MaxValue)
                bitmap.DecodePixelWidth = maxWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.Rotation = rotation;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
