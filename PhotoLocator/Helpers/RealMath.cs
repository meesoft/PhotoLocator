using System;
using System.Runtime.CompilerServices;

namespace PhotoLocator.Helpers
{
    public static class RealMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRange(double value, double min, double max)
        {
            return value >= min && value <= max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sqr(double value)
        {
            return value * value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Smooth step edge between min and max
        /// </summary>
        public static float SmoothStep(float x, float min, float max)
        {
            if (x <= min)
                return 0;
            if (x >= max)
                return 1;
            return SmoothStep((x - min) / (max - min));
        }
    }
}
