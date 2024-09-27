using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class GeneralFileFormatHandler
    {
        public const string SaveImageFilter = "JPEG|*.jpg|PNG|*.png|TIFF|*.tif|JPEG XR lossless|*.jxr|BMP|*.bmp";

        public static BitmapSource LoadFromStream(Stream source, Rotation rotation, int maxPixelWidth, bool preservePixelFormat, CancellationToken ct)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = source;
            if (maxPixelWidth < int.MaxValue)
                bitmap.DecodePixelWidth = maxPixelWidth;
            if (preservePixelFormat)
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.Rotation = rotation;
            bitmap.EndInit();
            ct.ThrowIfCancellationRequested();
            bitmap.Freeze();
            ct.ThrowIfCancellationRequested();
            return bitmap;
        }

        public static void SaveToFile(BitmapSource image, string outPath, BitmapMetadata? metadata = null)
        {
            var ext = Path.GetExtension(outPath).ToUpperInvariant();
            BitmapEncoder encoder;
            if (ext is ".JPG" or ".JPEG")
                encoder = new JpegBitmapEncoder() { QualityLevel = 95 };
            else if (ext is ".TIF" or ".TIFF")
                encoder = new TiffBitmapEncoder(); // Default is best compression
            else if (ext is ".PNG")
                encoder = new PngBitmapEncoder();
            else if (ext is ".BMP")
                encoder = new BmpBitmapEncoder();
            else if (ext is ".JXR")
                encoder = new WmpBitmapEncoder() { Lossless = true };
            else
                throw new UserMessageException("Unsupported file format " + ext);

            if (metadata is null)
                encoder.Frames.Add(BitmapFrame.Create(image));
            else
                encoder.Frames.Add(BitmapFrame.Create(image, null, ExifHandler.CreateMetadataForEncoder(metadata, encoder), null));
            
            using var fileStream = new FileStream(outPath, FileMode.Create);
            encoder.Save(fileStream);
        }
    }
}
