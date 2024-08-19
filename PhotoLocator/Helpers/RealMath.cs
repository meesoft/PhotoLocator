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

        public static double SmoothStep(double x)
        {
            return (3.0 - 2.0 * x) * x * x;
        }

        public static float SmoothStep(float x)
        {
            return (3.0f - 2.0f * x) * x * x;
        }


        /// <summary>
        /// Smooth step edge between xmin and xmax
        /// </summary>
        public static float SmoothStep(float xmin, float xmax, float x)
        {
            if (x <= xmin)
                return 0;
            if (x >= xmax)
                return 1;
            return SmoothStep((x - xmin) / (xmax - xmin));
        }
    }
}
