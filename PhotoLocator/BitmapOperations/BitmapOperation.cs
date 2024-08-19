#nullable disable

namespace PhotoLocator.BitmapOperations
{
    public abstract class OperationBase
    {
        /// <summary>
        /// DstBitmap holds the result of the operation after calling Apply
        /// </summary>
        public FloatBitmap DstBitmap { get; set; }

        /// <summary>
        /// SrcBitmap is the input image. If SrcBitmap is null then DstBitmap is used as both input and output
        /// </summary>
        public FloatBitmap SrcBitmap { get; set; }

        public virtual void SourceChanged() { }

        public abstract void Apply();
    }
}