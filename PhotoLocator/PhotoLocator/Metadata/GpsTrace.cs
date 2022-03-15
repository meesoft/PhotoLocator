using MapControl;
using SampleApplication;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace PhotoLocator.Metadata
{
    class GpsTrace : PolylineItem
    {
        public readonly List<DateTime> TimeStamps = new();

        public Location? Center => Locations is null || Locations.Count == 0 ? null : new Location(
            Locations.Select(l => l.Latitude).Sum() / Locations.Count,
            Locations.Select(l => l.Longitude).Sum() / Locations.Count);

        public static GpsTrace DecodeGpxStream(Stream stream)
        {
            var trace = new GpsTrace();
            trace.Locations = new LocationCollection();
            var document = new XmlDocument();
            document.Load(stream);
            var gpx = document["gpx"] ?? throw new Exception("gpx node missing");
            var trk = gpx["trk"] ?? throw new Exception("trk node missing");
            foreach(XmlNode trkseg in trk)
                if (trkseg.Name == "trkseg")
                    foreach (XmlNode trkpt in trkseg)
                        if (trkpt.Name == "trkpt")
                        {
                            var lat = trkpt.Attributes?["lat"];
                            var lon = trkpt.Attributes?["lon"];
                            var time = trkpt["time"];
                            if (lat != null && lon != null && time != null)
                            {
                                trace.Locations.Add(new Location(
                                    double.Parse(lat.InnerText, CultureInfo.InvariantCulture),
                                    double.Parse(lon.InnerText, CultureInfo.InvariantCulture)));
                                trace.TimeStamps.Add(DateTime.Parse(time.InnerText));
                            }
                        }
            return trace;
        }

        public static GpsTrace DecodeGpxFile(string fileName)
        {
            using var file = File.OpenRead(fileName);
            return DecodeGpxStream(file);
        }
    }
}
