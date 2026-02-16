using System.Xml;

namespace PhotoLocator.Gps
{
    [TestClass]
    public class GpsTraceTest
    {
        [TestMethod]
        public void DecodeGpxStream_ShouldDecodeGpx1()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.2022-07-02_16-19.gpx")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = GpxDecoder.DecodeStream(stream).Single();

            Assert.HasCount(244, trace.Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
            Assert.AreEqual(new DateTime(2022, 7, 2, 14, 19, 27, 0, DateTimeKind.Utc), trace.TimeStamps[0]);
        }

        [TestMethod]
        public void DecodeGpxStream_ShouldDecodeGpx2()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.20250503.gpx")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = GpxDecoder.DecodeStream(stream).Single();

            Assert.HasCount(2, trace.Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
            Assert.AreEqual(new DateTime(2025, 5, 3, 19, 38, 20, 388, DateTimeKind.Utc), trace.TimeStamps[0]);
        }

        [TestMethod]
        public void DecodeKmlStream_ShouldDecodeKml1()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.history-2016-05-17.kml")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = KmlDecoder.DecodeStream(stream, TimeSpan.FromMinutes(15)).Single();

            Assert.HasCount(540, trace.Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
            Assert.AreEqual(new DateTime(2016, 5, 17, 0, 13, 49, 841, DateTimeKind.Utc), trace.TimeStamps[0]);
        }

        [TestMethod]
        public void DecodeKmlStream_ShouldDecodeKml2()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.history-2022-07-09.kml")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = KmlDecoder.DecodeStream(stream, TimeSpan.FromMinutes(15)).Single();

            Assert.HasCount(259, trace.Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
            Assert.AreEqual(new DateTime(2022, 7, 8, 18, 14, 28, 899, DateTimeKind.Utc), trace.TimeStamps[0]);
        }

        [TestMethod]
        public void DecodeKmlStream_ShouldDecodeKml3()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(@"PhotoLocator.TestData.20250503.kml")
                ?? throw new FileNotFoundException("Resource not found");

            var trace = KmlDecoder.DecodeStream(stream, TimeSpan.FromMinutes(15)).Single();

            Assert.HasCount(2, trace.Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
        }

        [TestMethod]
        public void TimelineDecoder_ShouldDecodeTimeline()
        {
            const string TimelineJsonPath = @"TestData\Timeline.json";

            if (!File.Exists(TimelineJsonPath))
                Assert.Inconclusive("Test data not available");

            using var stream = File.OpenRead(TimelineJsonPath);
            var trace = TimelineDecoder.DecodeStream(stream).ToArray();

            Assert.HasCount(2, trace[0].Locations);
            Assert.AreEqual(DateTimeKind.Utc, trace[0].TimeStamps[0].Kind);
        }

        [TestMethod]
        public void DecodeGeoJsonStream_ShouldDecodeGeoJson()
        {
            using var stream = File.OpenRead(@"TestData\20250505.geojson");
            var traces = GeoJsonDecoder.DecodeStream(stream).ToArray();

            Assert.HasCount(1, traces);
            var trace = traces[0];

            Assert.HasCount(29, trace.Locations);
            Assert.AreEqual(23.1313316, trace.Locations[0].Latitude, 1e-6);
            Assert.AreEqual(43.8938363, trace.Locations[0].Longitude, 1e-6);
            Assert.AreEqual(DateTimeKind.Utc, trace.TimeStamps[0].Kind);
            Assert.AreEqual(new DateTime(2025, 5, 5, 5, 46, 22, 318, DateTimeKind.Utc), trace.TimeStamps[0]);
        }

        [TestMethod, Ignore("Not a test")]
        public void ConvertHtmlTableToGpx()
        {
            const string Path = @"C:\temp\";

            var src = new XmlDocument();
            src.Load(Path + "Fredede fortidsminder i Slagelse Kommune.xml");
            var root = src["doc"] ?? throw new FileFormatException("doc node missing");

            var settings = new XmlWriterSettings { Indent = true };
            using var dst = XmlWriter.Create(Path + "Fredede fortidsminder i Slagelse Kommune.gpx", settings);
            dst.WriteStartDocument();
            dst.WriteStartElement(null, "gpx", "http://www.topografix.com/GPX/1/1");
            foreach (var row in root.OfType<XmlNode>().Where(n => n.Name == "tr"))
            {
                var titleCol = row.ChildNodes[1] ?? throw new FileFormatException("title col missing");
                var refCol = row.ChildNodes[4] ?? throw new FileFormatException("ref col missing");
                var gpsCol = row.ChildNodes[5] ?? throw new FileFormatException("gps col missing");
                
                var title = titleCol["a"]?.InnerText + " " + refCol["a"]?.InnerText;

                var gpsText = gpsCol["span"]?["a"]?.InnerText ?? throw new FileFormatException("gps coordinates missing");
                var gpsParts = gpsText.Split("/")[2].Split(";", StringSplitOptions.TrimEntries);
                var lat = gpsParts[0];
                var lon = gpsParts[1].Split(" ", StringSplitOptions.TrimEntries)[0].TrimEnd((char)65279);

                dst.WriteStartElement("trk");
                {
                    dst.WriteElementString("name", title);
                    dst.WriteStartElement("trkseg");
                    {
                        dst.WriteStartElement("trkpt");
                        {
                            dst.WriteAttributeString("lat", lat);
                            dst.WriteAttributeString("lon", lon);
                            dst.WriteElementString("time", "2000-01-01");
                        }
                        dst.WriteEndElement();
                    }
                    dst.WriteEndElement();
                }
                dst.WriteEndElement();
            }
            dst.WriteEndElement();
            dst.WriteEndDocument();
        }
    }
}