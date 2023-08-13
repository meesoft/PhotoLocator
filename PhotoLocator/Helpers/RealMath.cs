using System;

namespace PhotoLocator.Helpers
{
    public static class RealMath
    {
        public static float EnsureRange(float value, float min, float max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }

        public static double EnsureRange(double value, double min, double max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }

        public static bool InRange(double value, double min, double max)
        {
            return value >= min && value <= max;
        }

        public static double Sqr(double value)
        {
            return value * value;
        }

        public static float Sqr(float value)
        {
            return value * value;
        }
    }
}
