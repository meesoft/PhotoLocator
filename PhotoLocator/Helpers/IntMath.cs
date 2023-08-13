using System;

namespace PhotoLocator.Helpers
{
    static class IntMath
    {
        public static int Round(float a)
        {
            return (int)Math.Round(a);
        }

        public static int Round(double a)
        {
            return (int)Math.Round(a);
        }

        public static int EnsureRange(int value, int min, int max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }
    }
}
