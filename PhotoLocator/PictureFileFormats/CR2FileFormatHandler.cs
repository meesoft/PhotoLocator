using System;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    public static class CR2FileFormatHandler
    {
        /// <summary>
        /// Takes extension in lower case including .
        /// </summary>
        public static bool CanLoad(string extension)
        {
            return extension == ".cr2";
        }

        public static BitmapSource LoadFromStream(Stream stream, Rotation rotation, int maxWidth, CancellationToken ct)
        {
            using var reader = new BinaryReader(stream);
            if (reader.ReadByte() == (byte)'I' && reader.ReadByte() == (byte)'I' && reader.ReadInt16() == 42)
            {
                var ifdOffset = reader.ReadInt32();
                while (ifdOffset > 0)
                {
                    var imageSize = 0;
                    var imageOffset = 0;
                    var width = 0;
                    var height = 0;
                    var compression = -1;
                    stream.Position = ifdOffset;
                    var ifdFieldCount = reader.ReadInt16();
                    for (var i = 0; i < ifdFieldCount; i++)
                    {
                        var tag = reader.ReadInt16();
                        var fieldType = reader.ReadInt16();
                        var valueCount = reader.ReadInt32();
                        var valueOrOffset = reader.ReadInt32();
                        switch (tag)
                        {
                            case 0x100:
                                width = valueOrOffset;
                                break;
                            case 0x101:
                                height = valueOrOffset;
                                break;
                            case 0x103:
                                compression = valueOrOffset;
                                break;
                            case 0x111:
                                imageOffset = valueOrOffset;
                                break;
                            case 0x112:
                                rotation = valueOrOffset switch
                                {
                                    3 => Rotation.Rotate180,
                                    6 => Rotation.Rotate90,
                                    8 => Rotation.Rotate270,
                                    _ => Rotation.Rotate0
                                };
                                break;
                            case 0x117:
                                imageSize = valueOrOffset;
                                break;
                        }
                    }
                    ct.ThrowIfCancellationRequested();
                    if (imageSize > 0 && imageOffset > 0 && width > 0 && height > 0 && compression >= 0)
                    {
                        if (compression == 6) // JPEG preview image
                        {
                            stream.Position = imageOffset;
                            var buf = new byte[imageSize];
                            stream.Read(buf, 0, imageSize);
                            return GeneralFileFormatHandler.LoadFromStream(new MemoryStream(buf, false), rotation, maxWidth, ct);
                        }
                        // Unknown compression, try general reader on whole file
                        stream.Position = 0;
                        return GeneralFileFormatHandler.LoadFromStream(stream, rotation, maxWidth, ct);
                    }
                    ifdOffset = reader.ReadInt32(); // Get next IFD offset
                }
            }
            throw new FormatException();
        }
    }
}

