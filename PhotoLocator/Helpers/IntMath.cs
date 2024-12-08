using System;
using System.Runtime.CompilerServices;

namespace PhotoLocator.Helpers
{
    static class IntMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Round(float a)
        {
            return (int)Math.Round(a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Round(double a)
        {
            return (int)Math.Round(a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
        {
            if (value <= min)
                return min;
            if (value >= max)
                return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRange(int value, int min, int max)
        {
            return (value >= min) && (value <= max);
        }
    }
}
