using MapControl;
using PhotoLocator.MapDisplay;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace PhotoLocator.Metadata
{
    internal class GpsTrace : PolylineItem
    {
        public readonly List<DateTime> TimeStamps = new();

        public Location? Center => Locations is null || Locations.Count == 0 ? null : new Location(
            Locations.Select(l => l.Latitude).Sum() / Locations.Count,
            Locations.Select(l => l.Longitude).Sum() / Locations.Count);

        public static GpsTrace DecodeGpxStream(Stream stream)
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

        class Placemark
        {
            public string? Name;
            public DateTime StartTime, EndTime;
            public readonly List<Location> Coordinates = new();
        }

        static double Interpolate(double val1, double val2, double alpha)
        {
            return (val1 * (1 - alpha) + val2 * alpha);
        }

        public static GpsTrace DecodeKmlStream(Stream stream, TimeSpan minimumInterval)
        {
            var document = new XmlDocument();
            document.Load(stream);
            var placemarks = new List<Placemark>();
            var kml = document["kml"] ?? throw new FileFormatException("kml node missing");
            foreach (var node in kml.FirstChild!.ChildNodes.Cast<XmlElement>().Where(n => n.Name == "Placemark"))
            {
                var placemark = new Placemark();
                placemark.Name = node["name"]?.InnerText;

                var timeSpanNode = node["TimeSpan"] ?? throw new FileFormatException("TimeSpan node missing");
                placemark.StartTime = DateTime.Parse(timeSpanNode["begin"]!.InnerText, CultureInfo.InvariantCulture);
                placemark.EndTime = DateTime.Parse(timeSpanNode["end"]!.InnerText, CultureInfo.InvariantCulture);

                var coordContainerNode = node["LineString"] ?? node["Point"]!;
                var coordsNode = coordContainerNode["coordinates"] ?? throw new FileFormatException("coordinates node missing");
                foreach (var coord in coordsNode.InnerText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var coords = coord.Split(',');
                    placemark.Coordinates.Add(new Location(
                        double.Parse(coords[1], CultureInfo.InvariantCulture),
                        double.Parse(coords[0], CultureInfo.InvariantCulture)));
                }
                placemarks.Add(placemark);
            }

            double minInterval = minimumInterval.TotalHours / 24;
            var trace = new GpsTrace();
            foreach (var placemark in placemarks)
            {
                var startTime = placemark.StartTime.ToOADate();
                var endTime = placemark.EndTime.ToOADate();
                if (placemark.Coordinates.Count == 1)
                {
                    var coord = placemark.Coordinates[0];
                    for (var time = startTime; time <= endTime; time += minInterval)
                    {
                        trace.Locations.Add(coord);
                        trace.TimeStamps.Add(DateTime.SpecifyKind(DateTime.FromOADate(time), placemark.StartTime.Kind));
                    }
                }
                else
                {
                    for (int i = 0; i < placemark.Coordinates.Count; i++)
                    {
                        trace.Locations.Add(placemark.Coordinates[i]);
                        trace.TimeStamps.Add(DateTime.SpecifyKind(DateTime.FromOADate(
                            Interpolate(startTime, endTime, (double)i / (placemark.Coordinates.Count - 1))), placemark.StartTime.Kind));
                    }
                }
            };
            return trace;
        }

        public static GpsTrace DecodeGpsTraceFile(string fileName, TimeSpan mininumInterval)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            using var file = File.OpenRead(fileName);
            if (ext == ".gpx")
                return DecodeGpxStream(file);
            if (ext == ".kml")
                return DecodeKmlStream(file, mininumInterval);
            throw new FileFormatException("Unsupported file format");
        }
    }
}
