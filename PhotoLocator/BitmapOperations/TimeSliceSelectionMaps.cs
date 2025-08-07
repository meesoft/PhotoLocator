using PhotoLocator.Helpers;
using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    delegate double SelectionMapFunction(double x, double y);

    static class TimeSliceSelectionMaps
    {
        public static readonly SelectionMapFunction LeftToRight = (x, y) => x;

        public static readonly SelectionMapFunction RightToLeft = (x, y) => 1 - x;

        public static readonly SelectionMapFunction TopToBottom = (x, y) => 1 - y;

        public static readonly SelectionMapFunction BottomToTop = (x, y) => y;

        public static readonly SelectionMapFunction TopLeftToBottomRight = (x, y) => (x + y) / 2;

        public static readonly SelectionMapFunction TopRightToBottomLeft = (x, y) => (1 - x + y) / 2;

        public static readonly SelectionMapFunction Ellipse = (x, y) => Math.Max(0, 1 - 2 * Math.Sqrt(RealMath.Sqr(x - 0.5) + RealMath.Sqr(y - 0.5)));

        public static readonly SelectionMapFunction Clock = (x, y) => (Math.PI + Math.Atan2(x - 0.5, y - 0.5)) / (2 * Math.PI);

        public static FloatBitmap GenerateSelectionMap(int width, int height, SelectionMapFunction expression)
        {
            var selectionMap = new FloatBitmap(width, height, 1);
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                    selectionMap.Elements[y, x] = (float)expression(x / (double)(width - 1), y / (double)(height - 1));
            });
            return selectionMap;
        }
    }
}
