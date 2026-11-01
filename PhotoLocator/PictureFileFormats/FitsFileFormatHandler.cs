using nom.tam.fits;
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

    private static string BuildMetadataString(Dictionary<string, string> metadata, int height, int width)
    {
        var selectedStrings = new List<string>();
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
        return string.Join(", ", selectedStrings); ;
    }

    public static BitmapSource LoadFromFile(string fileName, out string metadataString, CancellationToken ct)
    {
        using var fits = new Fits(fileName, FileAccess.Read);
        var basicHdu = fits.ReadHDU();
        ImageHDU? hdu = null;
        while ((hdu = basicHdu as ImageHDU) is null)
            basicHdu = fits.ReadHDU();
        if (hdu is null)
            throw new FileFormatException("No image HDU found in FITS file.");

        var format = hdu.BitPix;
        var axes = hdu.Axes;
        var (channels, height, width) = (axes[0], axes[1], axes[2]);

        var metadata = new Dictionary<string, string>();
        foreach (DictionaryEntry data in hdu.Header)
            if (data.Key is string key && data.Value is HeaderCard value)
                metadata[key] = value.Value;
        metadataString = BuildMetadataString(metadata, height, width);

        if (format == 16 && channels == 1)
        {
            var offset = (int)hdu.BZero;
            //var scale = hdu.BScale;
            var ch0 = (ushort[,])hdu.GetChannel(0);
            var pixels = new ushort[width * height];
            Parallel.For(0, height, y =>
            {
                var iDst = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                    pixels[iDst++] = (ushort)(ch0[y, x] + offset);
                ct.ThrowIfCancellationRequested();
            });
            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, pixels, width * 2);
            result.Freeze();
            return result;
        }

        if (format == 16 && channels == 3)
        {
            var offset = (int)hdu.BZero;
            //var scale = hdu.BScale;
            var ch0 = (ushort[,])hdu.GetChannel(0);
            var ch1 = (ushort[,])hdu.GetChannel(1);
            var ch2 = (ushort[,])hdu.GetChannel(2);
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
            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb48, null, pixels, width * 2 * 3);
            result.Freeze();
            return result;
        }

        throw new FileFormatException("Unsupported FITS format: " + format + " bits with " + channels + " channels");
    }
}
