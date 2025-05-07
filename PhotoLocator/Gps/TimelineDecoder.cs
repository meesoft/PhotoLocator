using MapControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PhotoLocator.Gps
{
    static class TimelineDecoder
    {
        public static IEnumerable<GpsTrace> DecodeStream(Stream stream, TimeSpan minimumInterval)
        {
            var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("semanticSegments", out var segments))
                yield break;

            foreach (var segment in segments.EnumerateArray())
            {
                if (segment.TryGetProperty("timelinePath", out var timelinePath))
                {
                    var trace = new GpsTrace();
                    foreach (var location in timelinePath.EnumerateArray())
                        if (location.TryGetProperty("point", out var point) && location.TryGetProperty("time", out var time))
                        {
                            var coords = point.GetString()!.Split([',', '°'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                            trace.Locations.Add(new Location(
                                double.Parse(coords[0], CultureInfo.InvariantCulture),
                                double.Parse(coords[1], CultureInfo.InvariantCulture)));
                            trace.TimeStamps.Add(time.GetDateTimeOffset().UtcDateTime);
                        }
                    yield return trace;
                }
                //else if (segment.TryGetProperty("visit", out var visit)
                //    && visit.TryGetProperty("topCandidate", out var topCandidate)
                //    && topCandidate.TryGetProperty("placeLocation", out var placeLocation))
                //{
                //    var trace = new GpsTrace();
                //    var point = placeLocation.GetProperty("latLng").GetString()!.Split([',', '°'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                //    trace.Locations.Add(new Location(
                //        double.Parse(point[0], CultureInfo.InvariantCulture),
                //        double.Parse(point[1], CultureInfo.InvariantCulture)));

                //    var time = segment.GetProperty("startTime").GetDateTimeOffset();
                //    trace.TimeStamps.Add(time.UtcDateTime);

                //    trace.Name = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            
                //    yield return trace;
                //}
            }
        }
    }
}
