using MapControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace PhotoLocator.Gps
{
    static class KmlDecoder
    {
        class Placemark
        {
            public string? Name;
            public DateTime StartTime, EndTime;
            public readonly List<Location> Coordinates = new();
        }

        static double Interpolate(double val1, double val2, double alpha)
        {
            return val1 * (1 - alpha) + val2 * alpha;
        }

        public static GpsTrace DecodeStream(Stream stream, TimeSpan minimumInterval)
        {
            var document = new XmlDocument();
            document.Load(stream);
            var placemarks = new List<Placemark>();
            var kml = document["kml"] ?? throw new FileFormatException("kml node missing");
            foreach (var node in kml.FirstChild!.ChildNodes.Cast<XmlElement>().Where(n => n.Name == "Placemark"))
            {
                var gxTrack = node["gx:Track"];
                if (gxTrack is not null)
                {
                    var placemark = new Placemark();
                    foreach (var child in gxTrack.ChildNodes.Cast<XmlElement>())
                    {
                        if (child.Name == "when")
                        {
                            placemark.StartTime = DateTime.Parse(child.InnerText, CultureInfo.InvariantCulture);
                            placemark.EndTime = placemark.StartTime;
                        }
                        else if (child.Name == "gx:coord")
                        {
                            var coords = child.InnerText.Split(' ');
                            placemark.Coordinates.Add(new Location(
                                double.Parse(coords[1], CultureInfo.InvariantCulture),
                                double.Parse(coords[0], CultureInfo.InvariantCulture)));
                            if (placemark.StartTime != default)
                            {
                                placemarks.Add(placemark);
                                placemark = new Placemark();
                            }
                        }
                    }
                }
                else
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
            }

            var minInterval = minimumInterval.TotalHours / 24;
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
                    for (var i = 0; i < placemark.Coordinates.Count; i++)
                    {
                        trace.Locations.Add(placemark.Coordinates[i]);
                        trace.TimeStamps.Add(DateTime.SpecifyKind(DateTime.FromOADate(
                            Interpolate(startTime, endTime, (double)i / (placemark.Coordinates.Count - 1))), placemark.StartTime.Kind));
                    }
                }
            };
            return trace;
        }
    }
}
