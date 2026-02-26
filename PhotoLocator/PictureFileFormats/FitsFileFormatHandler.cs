using FITSReader;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    class FitsFileFormatHandler
    {
        /// <summary>
        /// Takes extension in lower case including .
        /// </summary>
        public static bool CanLoad(string extension)
        {
            return extension == ".fits";
        }

        public static BitmapSource LoadFromFile(string fileName, out string metadataString, CancellationToken ct)
        {
            var fitsFile = FITSFile.ReadFile(fileName);
            var header = fitsFile.Headers.Find(header => header.DataType == FITSDataType.Int16) ?? throw new FileFormatException();

            var metadata = new Dictionary<string,string>();
            foreach(var kvp in header.RawHeaders)
            {
                var parts = kvp.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    metadata[parts[0]] = parts[1];
            }
            var offset = int.Parse(metadata["BZERO"], CultureInfo.InvariantCulture);

            var width = header.Width;
            var height = header.Height;
            var rawPixels = header.RawData.AsSpan(header.DataStartIndex, header.DataEndIndex - header.DataStartIndex);

            var srcPixels16 = MemoryMarshal.Cast<byte, ushort>(rawPixels).ToArray();
            var dstPixels = new ushort[srcPixels16.Length];

            Parallel.For(0, height, y =>
            {
                var iSrc = y * width;
                var iDst = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    var p1 = srcPixels16[iSrc];
                    var p2 = (short)(p1 >> 8 | p1 << 8) + offset; // swap bytes and apply offset
                    var p3 = (ushort)int.Clamp(p2 * 50, 0, 65535);
                    dstPixels[iDst] = p3;

                    iSrc += 1;
                    iDst += 1;
                }
                ct.ThrowIfCancellationRequested();
            });

            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, dstPixels, width * 2);
            result.Freeze();

            metadataString =
                $"{metadata["OBJECT"].Split("'", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0]}, " +
                $"{metadata["FILTER"].Split("'", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0]}, " +
                $"{width}x{height}, " +
                $"{metadata["DATE-OBS"].Split("'", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0]}";

            return result;
        }
    }
}
