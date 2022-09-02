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
        static readonly byte[] _jpegHeader = new byte[] { 0xff, 0xd8, 0xff };

        public static BitmapSource LoadFromStream(Stream stream, Rotation rotation, int maxWidth, CancellationToken ct)
        {
            var buffer = new byte[65536];
            var length = stream.Read(buffer, 0, buffer.Length);
            while (length > 10)
            {
                ct.ThrowIfCancellationRequested();

                //TODO: There is a risk that the headers cross buffer block boundaries in which case we currently fail to find them
                var index = buffer.AsSpan(0, length).IndexOf(_previewHeader);
                if (index < 0)
                {
                    length = stream.Read(buffer, 0, buffer.Length);
                    continue;
                }
                var index2 = buffer.AsSpan(index, length - index).IndexOf(_jpegHeader);
                if (index2 < 0)
                {
                    length = stream.Read(buffer, 0, buffer.Length);
                    index2 = buffer.AsSpan(0, length).IndexOf(_jpegHeader);
                    if (index2 < 0)
                        continue;
                    index = 0;
                }
                stream.Position += index + index2 - length;
                return GeneralFileFormatHandler.LoadFromStream(new OffsetStreamReader(stream), rotation, maxWidth, ct);
            }
            throw new FileFormatException();
        }
    }
}

