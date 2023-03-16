using MapControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace PhotoLocator.Gps
{
    static class GpxDecoder
    {
        public static IEnumerable<GpsTrace> DecodeStream(Stream stream)
        {
            var document = new XmlDocument();
            document.Load(stream);
            var gpx = document["gpx"] ?? throw new FileFormatException("gpx node missing");
            foreach (var trk in gpx.OfType<XmlNode>().Where(n => n.Name == "trk"))
            {
                var name = trk["name"]?.InnerText;
                foreach (var trkseg in trk.OfType<XmlNode>().Where(n => n.Name == "trkseg"))
                {
                    var trace = new GpsTrace();
                    trace.Name = name;
                    foreach (var trkpt in trkseg.OfType<XmlNode>().Where(n => n.Name == "trkpt"))
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
                    if (trace.Locations.Count > 0)
                        yield return trace;
                }
            }
        }
    }
}
