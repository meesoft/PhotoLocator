using System;
using System.Globalization;

namespace PhotoLocator.Metadata
{
    /// <summary>
    /// EXIF Rational Type (pack 4-byte numerator and 4-byte denominator into 8 bytes
    /// </summary>
    public class Rational
    {
        public int Numerator { get; }     //numerator of exif rational
        public int Denominator { get; }   //denominator of exif rational
        public long Bytes { get; }  //8 bytes that form the exif rational value

        //form rational from a given 4-byte numerator and denominator
        public Rational(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Span<byte> bytes = stackalloc byte[8];  //create a byte array with 8 bytes
            BitConverter.GetBytes(Numerator).CopyTo(bytes);  //copy 4 bytes of num to location 0 in the byte array
            BitConverter.GetBytes(Denominator).CopyTo(bytes[4..]);  //copy 4 bytes of denominator to location 4 in the byte array
            Bytes = BitConverter.ToInt64(bytes);
        }

        //form rational from an Int64
        public Rational(long bytes) : this(BitConverter.GetBytes(bytes))
        {
        }

        //form rational from an UInt64
        public Rational(ulong bytes) : this(BitConverter.GetBytes(bytes))
        {
        }

        //form rational from an array of 8 bytes
        public Rational(byte[] bytes)
        {
            Bytes = BitConverter.ToInt64(bytes);
            //convert the 4 bytes from n into a 4-byte int (becomes the numerator of the rational)
            Numerator = BitConverter.ToInt32(bytes, 0);
            //convert the 4 bytes from d into a 4-byte int (becomes the denominator of the rational)
            Denominator = BitConverter.ToInt32(bytes, 4);
        }

        //convert the exif rational into a double value
        public double ToDouble()
        {
            //round the double value to 5 digits
            return Math.Round(Convert.ToDouble(Numerator) / Convert.ToDouble(Denominator), 5);
        }

        public static Rational? Decode(object? raw)
        {
            if (raw is ulong value2)
                return new Rational(value2);
            if (raw is long value1)
                return new Rational(value1);
            if (raw is byte[] bytes)
                return new Rational(bytes);
            if (raw is string str)
            {
                var fields = str.Split('/', StringSplitOptions.TrimEntries);
                if (int.TryParse(fields[0], CultureInfo.InvariantCulture, out var num) && int.TryParse(fields[1], CultureInfo.InvariantCulture, out var denom))
                    return new Rational(num, denom);
            }
            return null;
        }
    }

    /// <summary>
    /// Special rational class to handle the GPS three rational values  (degrees, minutes, seconds)
    /// </summary>
    public class GPSRational
    {
        public Rational Degrees { get; }
        public Rational Minutes { get; }
        public Rational Seconds { get; }
        public long[] Bytes { get; }  //becomes an array of 3 longs that represent hrs, minutes, seconds as 3 rationals
        public double AngleInDegrees { get; set; }  //latitude or longitude as decimal degrees

        //form the 3-rational exif value from an angle in decimal degrees
        public GPSRational(double angleInDeg)
        {
            const int secondsDenominator = 100;

            var absAngleInDeg = Math.Abs(angleInDeg);
            var degreesInt = (int)absAngleInDeg;
            absAngleInDeg -= degreesInt;
            var minutesInt = (int)(absAngleInDeg * 60.0);
            absAngleInDeg -= minutesInt / 60.0;
            var secondsInt = (int)(absAngleInDeg * 3600.0 * secondsDenominator + 0.50);

            Degrees = new Rational(degreesInt, 1);
            Minutes = new Rational(minutesInt, 1);
            Seconds = new Rational(secondsInt, secondsDenominator);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = [Degrees.Bytes, Minutes.Bytes, Seconds.Bytes];
        }

        //Form the GPSRational object from an array of 24 bytes
        public GPSRational(byte[] bytes)
        {
            var degBytes = new byte[8]; var minBytes = new byte[8]; var secBytes = new byte[8];

            //form the hours, minutes, seconds rational values from the input 24 bytes
            // first 8 are hours, second 8 are the minutes, third 8 are the seconds
            Array.Copy(bytes, 0, degBytes, 0, 8); Array.Copy(bytes, 8, minBytes, 0, 8); Array.Copy(bytes, 16, secBytes, 0, 8);

            Degrees = new Rational(degBytes);
            Minutes = new Rational(minBytes);
            Seconds = new Rational(secBytes);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = [Degrees.Bytes, Minutes.Bytes, Seconds.Bytes];
        }

        public GPSRational(long[] bytes)
        {
            Degrees = new Rational(bytes[0]);
            Minutes = new Rational(bytes[1]);
            Seconds = new Rational(bytes[2]);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = bytes;
        }

        public static GPSRational? Decode(object raw)
        {
            if (raw is long[] longs)
                return new GPSRational(longs);
            if (raw is byte[] bytes)
                return new GPSRational(bytes);
            return null;
        }
    }
}
