using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class CR2FileFormatHandler
    {
        /// <summary> Takes extension in lower case including . </summary>
        public static bool CanLoad(string extension)
        {
            return extension == ".cr2";
        }

        public static BitmapSource LoadFromStream(Stream stream, Rotation rotation, int maxWidth, bool preservePixelFormat, CancellationToken ct)
        {
            using var reader = new BinaryReader(stream);
            if (reader.ReadByte() == (byte)'I' && reader.ReadByte() == (byte)'I' && reader.ReadInt16() == 42)
            {
                var ifdOffset = reader.ReadUInt32();
                uint imageSize = 0;
                uint imageOffset = 0;
                uint width = 0;
                uint height = 0;
                var compression = -1;
                using var ifdDecoder = new IfdDecoder(stream, ifdOffset);
                foreach (var tag in ifdDecoder.EnumerateIfdTags())
                {
                    switch (tag.TagId)
                    {
                        case 0x100:
                            width = tag.ValueOrOffset;
                            break;
                        case 0x101:
                            height = tag.ValueOrOffset;
                            break;
                        case 0x103:
                            compression = (int)tag.ValueOrOffset;
                            break;
                        case 0x111:
                            imageOffset = tag.ValueOrOffset;
                            break;
                        case 0x112:
                            rotation = tag.ValueOrOffset switch
                            {
                                3 => Rotation.Rotate180,
                                6 => Rotation.Rotate90,
                                8 => Rotation.Rotate270,
                                _ => Rotation.Rotate0
                            };
                            break;
                        case 0x117:
                            imageSize = tag.ValueOrOffset;
                            break;
                    }
                    ct.ThrowIfCancellationRequested();
                    if (imageSize > 0 && imageOffset > 0 && width > 0 && height > 0 && compression >= 0)
                    {
                        if (compression == 6) // JPEG preview image
                        {
                            stream.Position = imageOffset;
                            var buf = new byte[imageSize];
                            stream.Read(buf, 0, (int)imageSize);
                            return GeneralFileFormatHandler.LoadFromStream(new MemoryStream(buf, false), rotation, maxWidth, preservePixelFormat, ct);
                        }
                        // Unknown compression, try general reader on whole file
                        stream.Position = 0;
                        return GeneralFileFormatHandler.LoadFromStream(stream, rotation, maxWidth, preservePixelFormat, ct);
                    }
                }
            }
            throw new FileFormatException();
        }
    }
}

