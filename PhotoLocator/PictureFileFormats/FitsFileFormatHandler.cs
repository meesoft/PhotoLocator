using nom.tam.fits;
using OpenCvSharp;
using PhotoLocator.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats;

class FitsFileFormatHandler
{
    /// <summary>
    /// Takes extension in lower case including .
    /// </summary>
    public static bool CanLoad(string extension)
    {
        return extension == ".fits";
    }

    private static string BuildMetadataString(ImageHDU hdu, int width, int height, DateTimeOffset? timeStamp)
    {
        var selectedStrings = new List<string>();
        string? value;
        if ((value = hdu.Instrument) is not null)
            selectedStrings.Add(value);
        if ((value = hdu.GetTrimmedString("FILTER")) is not null)
            selectedStrings.Add(value);
        if ((value = hdu.Object) is not null)
            selectedStrings.Add(value);
        if ((value = hdu.Header.FindCard("EXPTIME")?.Value) is not null)
            selectedStrings.Add(value.TrimEnd('.') + "s");
        selectedStrings.Add($"{width}x{height}");
        if (timeStamp.HasValue)
            selectedStrings.Add(ExifHandler.FormatTimestampForDisplay(timeStamp.Value));
        return string.Join(", ", selectedStrings);
    }

    /*private static string BuildMetadataString(ImageHDU hdu, int width, int height)
    {
        var selectedStrings = new List<string>();
        var metadata = BuildMetadataDictionary(hdu);

        if (metadata.TryGetValue("INSTRUME", out var ins))
            selectedStrings.Add(ins);
        if (metadata.TryGetValue("FILTER", out var fil))
            selectedStrings.Add(fil);
        if (metadata.TryGetValue("OBJECT", out var obj))
            selectedStrings.Add(obj);
        if (metadata.TryGetValue("EXPTIME", out var et))
            selectedStrings.Add(et.TrimEnd('.') + "s");
        selectedStrings.Add($"{width}x{height}");
        if (metadata.TryGetValue("DATE-OBS", out var dt))
            selectedStrings.Add(dt);
        return string.Join(", ", selectedStrings);
    }
    
    private static Dictionary<string, string> BuildMetadataDictionary(ImageHDU hdu)
    {
        var metadata = new Dictionary<string, string>();
        foreach (DictionaryEntry data in hdu.Header)
            if (data.Key is string key && data.Value is HeaderCard value)
                metadata[key] = value.Value;
        return metadata;
    }*/

    public static (BitmapSource bitmap, DateTimeOffset? timeStamp, string metadataString) LoadFromFile(string fileName, bool preservePixelFormatAndMetadata, CancellationToken ct)
    {
        using var fits = new Fits(fileName, FileAccess.Read);
        var basicHdu = fits.ReadHDU();
        ImageHDU? hdu = null;
        while (basicHdu is not null && (hdu = basicHdu as ImageHDU) is null)
            basicHdu = fits.ReadHDU();
        if (hdu is null)
            throw new FileFormatException("No supported image HDU found in FITS file.");

        var format = hdu.BitPix;
        var axes = hdu.Axes;
        var (channels, height, width) = axes.Length == 2 ? (1, axes[0], axes[1]) : (axes[0], axes[1], axes[2]);

        var observationDate = hdu.ObservationDate;
        DateTimeOffset? timeStamp = observationDate == default ? null : observationDate;
        var metadataString = BuildMetadataString(hdu, width, height, timeStamp);

        BitmapSource bitmap;
        if (format == BasicHDU.BITPIX_SHORT && channels == 1)
        {
            var offset = (int)hdu.BZero;
            //var scale = hdu.BScale;
            var ch0 = (ushort[,])hdu.GetChannel(0);
            if (preservePixelFormatAndMetadata)
            {

                var pixels = new ushort[width * height];
                Parallel.For(0, height, y =>
                {
                    var iDst = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                        pixels[iDst++] = (ushort)(ch0[y, x] + offset);
                    ct.ThrowIfCancellationRequested();
                });
                bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, pixels, width * 2);
            }
            else
            {
                var pixels = new byte[width * height];
                Parallel.For(0, height, y =>
                {
                    var iDst = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                        pixels[iDst++] = (byte)((ch0[y, x] + offset) >> 8);
                    ct.ThrowIfCancellationRequested();
                });
                bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
            }
        }
        else if (format == BasicHDU.BITPIX_SHORT && channels == 3)
        {
            var offset = (int)hdu.BZero;
            //var scale = hdu.BScale;
            var ch0 = (ushort[,])hdu.GetChannel(0);
            var ch1 = (ushort[,])hdu.GetChannel(1);
            var ch2 = (ushort[,])hdu.GetChannel(2);

            if (preservePixelFormatAndMetadata)
            {
                var pixels = new ushort[width * height * 3];
                Parallel.For(0, height, y =>
                {
                    var iDst = (height - 1 - y) * width * 3;
                    for (int x = 0; x < width; x++)
                    {
                        pixels[iDst++] = (ushort)(ch0[y, x] + offset);
                        pixels[iDst++] = (ushort)(ch1[y, x] + offset);
                        pixels[iDst++] = (ushort)(ch2[y, x] + offset);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb48, null, pixels, width * 2 * 3);
            }
            else
            {
                var pixels = new byte[width * height * 3];
                Parallel.For(0, height, y =>
                {
                    var iDst = (height - 1 - y) * width * 3;
                    for (int x = 0; x < width; x++)
                    {
                        pixels[iDst++] = (byte)((ch0[y, x] + offset) >> 8);
                        pixels[iDst++] = (byte)((ch1[y, x] + offset) >> 8);
                        pixels[iDst++] = (byte)((ch2[y, x] + offset) >> 8);
                    }
                    ct.ThrowIfCancellationRequested();
                });
                bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3);
            }
        }
        else
            throw new FileFormatException("Unsupported FITS format: " + format + " with " + channels + " channels");

        bitmap.Freeze();
        if (preservePixelFormatAndMetadata)
        {
            var metadata = new BitmapMetadata("jpg");
            metadata.CameraModel = hdu.Instrument;
            metadata.Subject = hdu.Object;
            
            if (timeStamp.HasValue)
                ExifHandler.SetDateTaken(metadata, timeStamp.Value);

            var expTimeStr = hdu.Header.FindCard("EXPTIME")?.Value;
            if (expTimeStr is not null && int.TryParse(expTimeStr.TrimEnd('.'), out var expTime))
                metadata.SetQuery(ExifHandler.ExposureTimeQuery1, new Rational(expTime, 1).Bytes);
            metadata.Freeze();

            var result = BitmapFrame.Create(bitmap, thumbnail: null, metadata, colorContexts: null);
            result.Freeze();
            return (result, timeStamp, metadataString);
        }
        return (bitmap, timeStamp, metadataString);
    }
}
