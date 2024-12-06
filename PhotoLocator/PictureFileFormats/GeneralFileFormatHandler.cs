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

        static string? _jpegliPath;
        static bool _jpegliChecked;

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

        public static void SaveToFile(BitmapSource image, string targetPath, BitmapMetadata? metadata = null, int jpegQuality = 95)
        {
            var ext = Path.GetExtension(targetPath).ToUpperInvariant();
            BitmapEncoder encoder;
            if (ext is ".JPG" or ".JPEG")
            {
                if (!_jpegliChecked)
                {
                    _jpegliPath = Path.Combine(Path.GetDirectoryName(typeof(GeneralFileFormatHandler).Assembly.Location)!, "cjpegli.exe");
                    if (!File.Exists(_jpegliPath))
                        _jpegliPath = null;
                    _jpegliChecked = true;
                }
                if (_jpegliPath is not null)
                {
                    JpegliEncoder.SaveToFile(image, targetPath, metadata, jpegQuality, _jpegliPath);
                    return;
                }
                encoder = new JpegBitmapEncoder() { QualityLevel = jpegQuality };
            }
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
            
            using var fileStream = new FileStream(targetPath, FileMode.Create);
            encoder.Save(fileStream);
        }
    }
}
