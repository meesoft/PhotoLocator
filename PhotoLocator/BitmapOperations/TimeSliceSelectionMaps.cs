using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    delegate double SelectionMapFunction(double x, double y);

    class TimeSliceSelectionMaps
    {
        public SelectionMapFunction? SelectionMapExpression { get; set; }

        public static SelectionMapFunction LeftToRight = (x, y) => x;

        public static SelectionMapFunction RightToLeft = (x, y) => 1 - x;

        public static SelectionMapFunction TopToBottom = (x, y) => 1 - y;

        public static SelectionMapFunction BottomToTop = (x, y) => y;

        public static SelectionMapFunction TopLeftToBottomRight = (x, y) => (x + y) / 2;

        public static SelectionMapFunction TopRightToBottomLeft = (x, y) => (1 - x + y) / 2;

        public static SelectionMapFunction Sphere = (x, y) => Math.Sqrt(Math.Pow(x - 0.5, 2) + Math.Pow(y - 0.5, 2));

        public static double Clock(double x, double y)
        {
            double angle = Math.Atan2(y - 0.5, x - 0.5);
            return (angle + Math.PI) / (2 * Math.PI);
        }

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
