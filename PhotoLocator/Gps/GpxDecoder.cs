using MapControl;
using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace PhotoLocator.Gps
{
    static class GpxDecoder
    {
        public static GpsTrace DecodeStream(Stream stream)
        {
            var trace = new GpsTrace();
            var document = new XmlDocument();
            document.Load(stream);
            var gpx = document["gpx"] ?? throw new FileFormatException("gpx node missing");
            var trk = gpx["trk"] ?? throw new FileFormatException("trk node missing");
            foreach (XmlNode trkseg in trk)
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
                                trace.TimeStamps.Add(DateTime.Parse(time.InnerText, CultureInfo.InvariantCulture));
                            }
                        }
            return trace;
        }
    }
}
