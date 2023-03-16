namespace PhotoLocator.Gps
{
    [TestClass]
    public class GpsTraceTest
    {
        [TestMethod]
        public void DecodeGpxStream_ShouldDecodeGpx()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-07-02_16-19.gpx")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = GpxDecoder.DecodeStream(stream).Single();

            Assert.AreEqual(244, trace.Locations.Count);
        }

        [TestMethod]
        public void DecodeKmlStream_ShouldDecodeKml1()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.history-2016-05-17.kml")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = KmlDecoder.DecodeStream(stream, TimeSpan.FromMinutes(15)).Single();

            Assert.AreEqual(540, trace.Locations.Count);
        }

        [TestMethod]
        public void DecodeKmlStream_ShouldDecodeKml2()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.history-2022-07-09.kml")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = KmlDecoder.DecodeStream(stream, TimeSpan.FromMinutes(15)).Single();

            Assert.AreEqual(259, trace.Locations.Count);
        }
    }
}