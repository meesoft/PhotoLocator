using PhotoshopFile;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.PictureFileFormats
{
    static class PhotoshopFileFormatHandler
    {
        /// <summary>
        /// Takes extension in lower case including .
        /// </summary>
        public static bool CanLoad(string extension)
        {
            return extension == ".psd";
        }

        public static BitmapSource LoadFromStream(Stream stream, Rotation rotation, int maxWidth, bool preservePixelFormat, CancellationToken ct)
        {
            var psd = new PsdFile(stream, new LoadContext());
            foreach (var psdLayer in (new[] { psd.BaseLayer }).Concat(psd.Layers))
            {
                if (psdLayer != psd.BaseLayer && (!psdLayer.Visible || psdLayer.Opacity == 0) 
                    || psdLayer.Rect.Width == 0 || psdLayer.Rect.Height == 0)
                    continue;
                var bitmap = CreateLayerBitmap(psd, psdLayer);
                bitmap.Freeze();
                return bitmap;
            }
            throw new FileFormatException();
        }

        public static BitmapSource CreateLayerBitmap(PsdFile psd, Layer layer)
        {
            if (psd.ColorMode == PsdColorMode.Grayscale)
            {
                if (psd.BitDepth == 8)
                    return BitmapSource.Create(layer.Rect.Width, layer.Rect.Height, 96, 96, PixelFormats.Gray8, null,
                        layer.Channels.GetId(0).ImageData, layer.Rect.Width);
                if (psd.BitDepth == 16)
                    return BitmapSource.Create(layer.Rect.Width, layer.Rect.Height, 96, 96, PixelFormats.Gray16, null,
                        layer.Channels.GetId(0).ImageData, layer.Rect.Width * 2);
            }
            else if (psd.ColorMode == PsdColorMode.RGB)
            {
                if (psd.BitDepth == 8)
                {
                    var pixels = new byte[layer.Rect.Width * layer.Rect.Height * 3];
                    Parallel.For(0, 3, ch => GetChannelPixels8(layer.Channels.GetId(ch), pixels, ch, 3));
                    return BitmapSource.Create(layer.Rect.Width, layer.Rect.Height, 96, 96, PixelFormats.Rgb24, null,
                        pixels, layer.Rect.Width * 3);
                }
                if (psd.BitDepth == 16)
                {
                    var pixels = new byte[layer.Rect.Width * layer.Rect.Height * 6];
                    Parallel.For(0, 3, ch => GetChannelPixels16(layer.Channels.GetId(ch), pixels, ch * 2, 6));
                    return BitmapSource.Create(layer.Rect.Width, layer.Rect.Height, 96, 96, PixelFormats.Rgb48, null,
                        pixels, layer.Rect.Width * 6);
                }
            }
            else if (psd.ColorMode == PsdColorMode.CMYK)
            {
                if (psd.BitDepth == 8)
                {
                    var pixels = new byte[layer.Rect.Width * layer.Rect.Height * 4];
                    Parallel.For(0, 4, ch => GetChannelPixels8(layer.Channels.GetId(ch), pixels, ch, 4));
                    return BitmapSource.Create(layer.Rect.Width, layer.Rect.Height, 96, 96, PixelFormats.Cmyk32, null,
                        pixels, layer.Rect.Width * 4);
                }
            }
            throw new NotSupportedException("Unsupported color mode");
        }

        private static void GetChannelPixels8(Channel channel, byte[] dest, int offset, int dist)
        {
            var size = channel.Rect.Width * channel.Rect.Height;
            var source = channel.ImageData;
            for (int iSrc = 0, iDst = offset; iSrc < size; iSrc++, iDst += dist)
                dest[iDst] = source[iSrc];
        }

        private static void GetChannelPixels16(Channel channel, byte[] dest, int offset, int dist)
        {
            dist -= 2;
            var size = channel.Rect.Width * channel.Rect.Height * 2;
            var source = channel.ImageData;
            for (int iSrc = 0, iDst = offset; iSrc < size;)
            {
                dest[iDst++] = source[iSrc++];
                dest[iDst++] = source[iSrc++];
                iDst += dist;
            }
        }
    }
}

