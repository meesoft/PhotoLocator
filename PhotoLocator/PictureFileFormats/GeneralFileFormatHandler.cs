using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.Settings;
using System;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class GeneralFileFormatHandler
    {
        public const string SaveImageFilter = "JPEG|*.jpg|PNG|*.png|TIFF|*.tif|JPEG XR lossless|*.jxr|JPEG XL|*.jxl|BMP|*.bmp";

        public const int DefaultJpegQuality = 90;

        static string? _jpegliPath;
        static bool _jpegliChecked;

        public static bool IsRawFile(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() is ".cr2" or ".cr3" or ".dng" or ".arw" or ".nef";
        }

        public static bool IsVideoFile(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() is ".mp4" or ".mov" or ".avi";
        }

        public static BitmapSource LoadFromStream(Stream source, Rotation rotation, int maxPixelWidth, bool preservePixelFormat, CancellationToken ct)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = source;
            if (maxPixelWidth < int.MaxValue)
                bitmap.DecodePixelWidth = maxPixelWidth;
            if (preservePixelFormat)
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.Rotation = rotation;
            ct.ThrowIfCancellationRequested();
            bitmap.EndInit();
            ct.ThrowIfCancellationRequested();
            bitmap.Freeze();
            ct.ThrowIfCancellationRequested();
            return bitmap;
        }

        public static void SaveToFile(BitmapSource bitmap, string targetPath, BitmapMetadata? metadata = null, ISettings? settings = null)
        {
            var ext = Path.GetExtension(targetPath).ToLowerInvariant();
            BitmapEncoder encoder;
            if (ext is ".jpg" or ".jpeg")
            {
                if (!_jpegliChecked)
                {
                    _jpegliPath = Path.Combine(AppContext.BaseDirectory, "jpegli", "cjpegli.exe");
                    if (!File.Exists(_jpegliPath))
                    {
                        Log.Write("jpegli not found at " + _jpegliPath);
                        _jpegliPath = null;
                    }
                    _jpegliChecked = true;
                }
                if (_jpegliPath is not null)
                {
                    Log.Write("Saving using " + _jpegliPath);
                    JpegliEncoder.SaveToFile(bitmap, targetPath, metadata, settings?.JpegQuality ?? DefaultJpegQuality, _jpegliPath);
                    return;
                }
                encoder = new JpegBitmapEncoder() { QualityLevel = settings?.JpegQuality ?? DefaultJpegQuality };
            }
            else if (ext is ".tif" or ".tiff")
                encoder = new TiffBitmapEncoder(); // Default is best compression
            else if (ext is ".png")
                encoder = new PngBitmapEncoder();
            else if (ext is ".bmp")
                encoder = new BmpBitmapEncoder();
            else if (ext is ".jxr")
                encoder = new WmpBitmapEncoder() { Lossless = true };
            else if (ext is ".jxl")
            {
                JpegXlFileFormatHandler.SaveToFile(bitmap, targetPath, metadata, settings);
                return;
            }
            else
                throw new UserMessageException("Unsupported file format " + ext);

            if (metadata is null)
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
            else
                encoder.Frames.Add(BitmapFrame.Create(bitmap, null, ExifHandler.CreateMetadataForEncoder(metadata, encoder), null));
            
            using var fileStream = new FileStream(targetPath, FileMode.Create);
            encoder.Save(fileStream);
        }
    }
}
