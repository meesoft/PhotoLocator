using System;
using System.Globalization;

namespace PhotoLocator.BitmapOperations
{
    /// <summary> Region of interest </summary>
    struct ROI
    {
        public int Left, Right, Top, Bottom;

        /// <summary> Region of interest </summary>
        public ROI(int left, int right, int top, int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        /// <summary> Single pixel region of interest (inclusive) </summary>
        public ROI(int x, int y)
        {
            Left = x;
            Right = x;
            Top = y;
            Bottom = y;
        }

        public void Offset(int x, int y)
        {
            Left += x;
            Right += x;
            Top += y;
            Bottom += y;
        }

        public override readonly string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "x:[{0} ; {1}] y:[{2} ; {3}]", Left, Right, Top, Bottom);
        }

        /// <summary>
        /// Width, exclusive
        /// </summary>
        public int Width { readonly get => Right - Left; set => Right = Left + value; }

        /// <summary>
        /// Height, exclusive
        /// </summary>
        public int Height { readonly get => Bottom - Top; set => Bottom = Top + value; }

        /// <summary>
        /// this=Intersect(this, other)
        /// </summary>
        public void Intersect(ref readonly ROI other)
        {
            Left = Math.Max(Left, other.Left);
            Top = Math.Max(Top, other.Top);
            Right = Math.Min(Right, other.Right);
            Bottom = Math.Min(Bottom, other.Bottom);
        }

        /// <summary>
        /// this=Union(this, other)
        /// </summary>
        public void Union(ref readonly ROI other)
        {
            if (other.Left < Left)
                Left = other.Left;
            if (other.Top < Top)
                Top = other.Top;
            if (other.Right > Right)
                Right = other.Right;
            if (other.Bottom > Bottom)
                Bottom = other.Bottom;
        }

        public readonly bool Equals(ref readonly ROI other)
        {
            return Left == other.Left && Right == other.Right && Top == other.Top && Bottom == other.Bottom;
        }
    }
}
