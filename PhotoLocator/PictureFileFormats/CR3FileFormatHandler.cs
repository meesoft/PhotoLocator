using System;
using System.IO;
using System.Text;
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

        public static BitmapSource? TryLoadFromStream(Stream source, Rotation rotation, int maxWidth)
        {
            var buffer = new byte[65536];
            while (true)
            {
                var length = source.Read(buffer, 0, buffer.Length);
                if (length < 10)
                    break;

                //TODO: There is a risk that the headers cross buffer block boundaries in which case we currently fail to find them
                var index = buffer.AsSpan(0, length).IndexOf(_previewHeader);
                if (index < 0)
                    continue;
                var index2 = buffer.AsSpan(index, length - index).IndexOf(_jpegHeader);
                if (index2 < 0)
                    continue;
                source.Position += index + index2 - length;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new OffsetStreamReader(source);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (maxWidth < int.MaxValue)
                    bitmap.DecodePixelWidth = maxWidth;
                bitmap.Rotation = rotation;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            return null;
        }
    }
}

