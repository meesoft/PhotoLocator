using System;

namespace PhotoLocator.Metadata
{
    /// <summary>
    /// EXIF Rational Type (pack 4-byte numerator and 4-byte denominator into 8 bytes
    /// </summary>
    public class Rational
    {
        public readonly int Num;     //numerator of exif rational
        public readonly int Denom;   //denominator of exif rational
        public readonly long Bytes;   //8 bytes that form the exif rational value

        //form rational from a given 4-byte numerator and denominator
        public Rational(int num, int denom)
        {
            Num = num;
            Denom = denom;
            var bytes = new byte[8];  //create a byte array with 8 bytes
            BitConverter.GetBytes(Num).CopyTo(bytes, 0);  //copy 4 bytes of num to location 0 in the byte array
            BitConverter.GetBytes(Denom).CopyTo(bytes, 4);  //copy 4 bytes of denom to location 4 in the byte array
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
            Num = BitConverter.ToInt32(bytes, 0);
            //convert the 4 bytes from d into a 4-byte int (becomes the denonimator of the rational)
            Denom = BitConverter.ToInt32(bytes, 4);
        }

        //convert the exif rational into a double value
        public double ToDouble()
        {
            //round the double value to 5 digits
            return Math.Round(Convert.ToDouble(Num) / Convert.ToDouble(Denom), 5);
        }

        public static Rational? Decode(object raw)
        {
            if (raw is ulong value2)
                return new Rational(value2);
            if (raw is long value1)
                return new Rational(value1);
            if (raw is byte[] bytes)
                return new Rational(bytes);
            return null;
        }
    }

    /// <summary>
    /// Special rational class to handle the GPS three rational values  (degrees, minutes, seconds)
    /// </summary>
    public class GPSRational
    {
        public readonly Rational Degrees;
        public readonly Rational Minutes;
        public readonly Rational Seconds;
        public readonly long[] Bytes;  //becomes an array of 3 longs that represent hrs, minutes, seconds as 3 rationals
        public double AngleInDegrees;  //latitude or longitude as decimal degrees

        //form the 3-rational exif value from an angle in decimal degrees
        public GPSRational(double angleInDeg)
        {
            //convert angle in decimal degrees to three rationals (deg, min, sec) with denominator of 1
            //NOTE:  this formulation results in a descretization of about 100 ft in the lat/lon position
            var absAngleInDeg = Math.Abs(angleInDeg);
            var degreesInt = (int)absAngleInDeg;
            absAngleInDeg -= degreesInt;
            var minutesInt = (int)(absAngleInDeg * 60.0);
            absAngleInDeg -= minutesInt / 60.0;
            var secondsInt = (int)(absAngleInDeg * 3600.0 + 0.50);

            //form a rational using "1" as the denominator
            var denominator = 1;
            Degrees = new Rational(degreesInt, denominator);
            Minutes = new Rational(minutesInt, denominator);
            Seconds = new Rational(secondsInt, denominator);

            AngleInDegrees = Degrees.ToDouble() + Minutes.ToDouble() / 60.0 + Seconds.ToDouble() / 3600.0;

            Bytes = new long[3] { Degrees.Bytes, Minutes.Bytes, Seconds.Bytes };
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

            Bytes = new long[3] { Degrees.Bytes, Minutes.Bytes, Seconds.Bytes };
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
