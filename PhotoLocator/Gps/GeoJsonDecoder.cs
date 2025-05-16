using MapControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PhotoLocator.Gps
{
    static class GeoJsonDecoder
    {
        public static IEnumerable<GpsTrace> DecodeStream(Stream stream)
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (root.GetProperty("type").GetString() != "FeatureCollection")
                throw new FileFormatException("Invalid GeoJSON format: FeatureCollection expected");

            var features = root.GetProperty("features").EnumerateArray();
            var trace = new GpsTrace();

            foreach (var feature in features)
            {
                var geometry = feature.GetProperty("geometry");
                if (geometry.GetProperty("type").GetString() != "Point")
                    continue;

                var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToArray();
                var lon = coordinates[0].GetDouble();
                var lat = coordinates[1].GetDouble();

                if (!feature.TryGetProperty("properties", out var properties))
                    continue; // Skip features without properties
                if (!properties.TryGetProperty("time", out var timeProperty))
                    continue; // Skip features without time
                var time = timeProperty.GetString();
                if (string.IsNullOrEmpty(time))
                    continue; // Skip features with null/empty time

                trace.Locations.Add(new Location(lat, lon));
                trace.TimeStamps.Add(DateTime.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
            }

            if (trace.Locations.Count > 0)
                yield return trace;
        }
    }
}