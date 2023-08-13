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

        /// <summary>
        /// Only works for positive numbers. Seems to be faster than Math.Pow()
        /// </summary>
        /// <returns>val^power</returns>
        public static float Pow(float val, float power)
        {
            return (float)Math.Exp(Math.Log(val) * power);
        }

        /// <summary>
        /// Only works for positive numbers. Seems to be faster than Math.Pow()
        /// </summary>
        /// <returns>val^power</returns>
        public static double Pow(double val, double power)
        {
            return Math.Exp(Math.Log(val) * power);
        }

        public static double Gauss(double x, double spread)
        {
            return Math.Exp(-Sqr(x / spread));
        }

        public static float Interpolate(int val1, int val2, float alpha)
        {
            //return val1 * (1 - alpha) + val2 * alpha;
            return val1 + (val2 - val1) * alpha;
        }

        public static float Interpolate(float val1, float val2, float alpha)
        {
            //return val1 * (1 - alpha) + val2 * alpha;
            return val1 + (val2 - val1) * alpha;
        }

        public static double Interpolate(double val1, double val2, double alpha)
        {
            //return val1 * (1 - alpha) + val2 * alpha;
            return val1 + (val2 - val1) * alpha;
        }

        /// <summary>
        /// Interpolate between val1 and val2 at alpha in [intStart;intEnd]
        /// </summary>
        public static float Interpolate(float val1, float val2, float alpha, float intStart, float intEnd)
        {
            return Interpolate(val1, val2, (alpha - intStart) / (intEnd - intStart));
        }

        /// <summary>
        /// Interpolate between val1 and val2 at alpha in [intStart;intEnd]
        /// </summary>
        public static double Interpolate(double val1, double val2, double alpha, double intStart, double intEnd)
        {
            return Interpolate(val1, val2, (alpha - intStart) / (intEnd - intStart));
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

        public static double TotalVariance(float[] data)
        {
            double sum = 0, sumSquares = 0;
            for (var i = 0; i < data.Length; i++)
            {
                sum += data[i];
                sumSquares += Sqr(data[i]);
            }
            return sumSquares - Sqr(sum) / data.Length;
        }

        /// <summary>
        /// Return positive value in 0-b
        /// </summary>
        public static float PositiveModulus(float a, float b)
        {
            return a < 0 ? b + a % b : a % b;
        }

        /// <summary>
        /// Return positive value in 0-b
        /// </summary>
        public static double PositiveModulus(double a, double b)
        {
            return a < 0 ? b + a % b : a % b;
        }
    }
}
