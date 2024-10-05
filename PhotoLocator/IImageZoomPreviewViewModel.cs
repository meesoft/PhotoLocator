using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    interface IImageZoomPreviewViewModel
    {
        BitmapSource? PreviewPictureSource { get; set; }

        /// <summary> Zoom level or 0 for auto </summary>
        int PreviewZoom { get; set; }
    }
}