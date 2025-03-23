using System;
using System.Diagnostics;
using System.Windows;

namespace PhotoLocator.Helpers
{
    [DebuggerDisplay("x={X} y={Y}")]
    struct Vector2d
    {
        public double X, Y;

        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Vector2d(Point pt)
        {
            X = pt.X;
            Y = pt.Y;
        }

        public static readonly Vector2d Zeros = new(0, 0);

        public static Vector2d operator +(Vector2d v1, Vector2d v2)
        {
            return new Vector2d(v1.X + v2.X, v1.Y + v2.Y);
        }

        public static Vector2d operator -(Vector2d v1, Vector2d v2)
        {
            return new Vector2d(v1.X - v2.X, v1.Y - v2.Y);
        }

        public readonly double Dist(Vector2d pt)
        {
            return Math.Sqrt(RealMath.Sqr(X - pt.X) + RealMath.Sqr(Y - pt.Y));
        }

        public readonly double Length()
        {
            return Math.Sqrt(X * X + Y * Y);
        }

        /// <summary>
        /// Angle in -Pi to Pi
        /// </summary>
        public readonly double Angle()
        {
            if (X > 0)
            {
                if (Y >= 0)
                    return Math.Atan(Y / X);
                else
                    return -Math.Atan(-Y / X);
            }
            if (X < 0)
            {
                if (Y >= 0)
                    return Math.PI - Math.Atan(-Y / X);
                else
                    return Math.Atan(Y / X) - Math.PI;
            }
            if (Y >= 0)
                return Math.PI / 2;
            else
                return -Math.PI / 2;
        }
    }
}