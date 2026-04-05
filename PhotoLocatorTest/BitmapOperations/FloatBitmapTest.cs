using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class FloatBitmapTest
    {
        [TestMethod]
        public void FloatToByteGammaLutRange_ShouldBeSufficient()
        {
            const int TestRange = 65536;
            double gamma = 2.2;

            var lut = FloatBitmap.CreateGammaLookupFloatToByte(gamma);
            var scale = 1.0 / (TestRange - 1);
            int diff = 0;
            for (int i = 0; i < TestRange; i++)
            {
                //float e = i / (float)TestRange;
                float e = (float)Math.Pow(i * scale, gamma);
                var expected = (byte)(Math.Pow(e, 1 / gamma) * 255 + 0.5);

                var lutApprox = lut[(int)(e * FloatBitmap.FloatToByteGammaLutRange + 0.5f)];
                var err = Math.Abs(expected - lutApprox);

                //var lutIndexFloat = e * FloatBitmap.FloatToByteGammaLutRange;
                //var lutIndexFloor = (int)lutIndexFloat;
                //var delta = lutIndexFloat - lutIndexFloor;
                //var lutApproxInterpolated = lutIndexFloor == FloatBitmap.FloatToByteGammaLutRange ? lut[lutIndexFloor] :
                //    (byte)(lut[lutIndexFloor] * (1 - delta) + lut[lutIndexFloor + 1] * (delta) + 0.5);
                //var err = Math.Abs(expected - lutApproxInterpolated);

                Assert.AreEqual(0, err, 1.0, e.ToString());
                diff += err;
            }
            Assert.AreEqual(0, diff, 308.0, "Sum of diffs");
            Console.WriteLine(diff);
        }

        [TestMethod]
        [DataRow(6, 4, 3, 1)]
        [DataRow(6000, 4000, 3, 1)]
        [DataRow(6000, 4000, 3, 2.2)]
        public void Assign_ShouldAssign(int width, int height, int sourcePixelSize, double gamma)
        {
            var format = sourcePixelSize switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Rgb24,
                4 => PixelFormats.Cmyk32,
                _ => throw new ArgumentException("Unsupported pixel size")
            };
            var sourcePixels = new byte[width * height * sourcePixelSize];
            for (int i = 0; i < sourcePixels.Length; i++)
                sourcePixels[i] = (byte)(i & 255);
            var source = BitmapSource.Create(width, height, 96, 96, format, null, sourcePixels, width * sourcePixelSize);
           
            var floatBitmap = new FloatBitmap();
            var sw = Stopwatch.StartNew();
            floatBitmap.Assign(source, gamma);
            Console.WriteLine(sw.ElapsedMilliseconds);

            Assert.AreEqual(width, floatBitmap.Width);
        }

        [TestMethod]
        [DataRow(6, 4, 1)]
        [DataRow(6000, 4000, 1)]
        [DataRow(6000, 4000, 2.2)]
        public void Assign_ShouldAssignBgr32(int width, int height, double gamma)
        {
            var sourcePixels = new byte[width * height * 4];
            for (int i = 0; i < sourcePixels.Length; i++)
                sourcePixels[i] = (byte)(i & 255);
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, sourcePixels, width * 4);

            var floatBitmap = new FloatBitmap();
            var sw = Stopwatch.StartNew();
            floatBitmap.Assign(source, gamma);
            Console.WriteLine(sw.ElapsedMilliseconds);

            Assert.AreEqual(width, floatBitmap.Width);

            var gammaLut = FloatBitmap.CreateDeGammaLookup(gamma, 256);
            Assert.AreEqual(gammaLut[sourcePixels[2]], floatBitmap.Elements[0, 0]);
            Assert.AreEqual(gammaLut[sourcePixels[1]], floatBitmap.Elements[0, 1]);
            Assert.AreEqual(gammaLut[sourcePixels[0]], floatBitmap.Elements[0, 2]);
        }

        [TestMethod]
        public void MinMax_ShouldReturnMinMax()
        {
            var floatBitmap = new FloatBitmap(6, 4, 3);
            floatBitmap[0, 0] = 1;

            var sw = Stopwatch.StartNew();
            var minMax = floatBitmap.MinMax();
            Console.WriteLine(sw.ElapsedMilliseconds);

            Assert.AreEqual(0, minMax.Min);
            Assert.AreEqual(1, minMax.Max);
        }

        [TestMethod]
        public void Mean_ShouldReturnMean()
        {
            var floatBitmap = new FloatBitmap(6, 4, 3);
            floatBitmap.ProcessElementWise(p => 1);

            var sw = Stopwatch.StartNew();
            var mean = floatBitmap.Mean();
            Console.WriteLine(sw.ElapsedMilliseconds);

            Assert.AreEqual(1, mean);
        }
    }
}
