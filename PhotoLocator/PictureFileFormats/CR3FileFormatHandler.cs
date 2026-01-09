using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class CR3FileFormatHandler
    {
        /// <summary>
        /// Takes extension in lower case including .
        /// </summary>
        public static bool CanLoad(string extension)
        {
            return extension == ".cr3";
        }

        static readonly byte[] _previewHeader = Encoding.ASCII.GetBytes("mdat");
        static readonly byte[] _jpegHeader = [0xff, 0xd8, 0xff];

        public static BitmapSource LoadFromStream(Stream stream, Rotation rotation, int maxWidth, bool preservePixelFormat, CancellationToken ct)
        {
            Span<byte> buffer = stackalloc byte[65536];
            var length = stream.Read(buffer);
            while (length > 10)
            {
                ct.ThrowIfCancellationRequested();

                //TODO: There is a risk that the headers cross buffer block boundaries in which case we currently fail to find them
                var index = buffer[..length].IndexOf(_previewHeader);
                if (index < 0)
                {
                    length = stream.Read(buffer);
                    continue;
                }
                var index2 = buffer[index..length].IndexOf(_jpegHeader);
                if (index2 < 0)
                {
                    length = stream.Read(buffer);
                    index2 = buffer[..length].IndexOf(_jpegHeader);
                    if (index2 < 0)
                        continue;
                    index = 0;
                }
                stream.Position += index + index2 - length;
                return GeneralFileFormatHandler.LoadFromStream(new OffsetStreamReader(stream), rotation, maxWidth, preservePixelFormat, ct);
            }
            throw new FileFormatException();
        }
    }
}

