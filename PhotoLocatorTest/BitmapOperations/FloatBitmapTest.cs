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
    }
}
