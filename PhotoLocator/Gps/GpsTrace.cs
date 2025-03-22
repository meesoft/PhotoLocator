﻿using MapControl;
using PhotoLocator.MapDisplay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PhotoLocator.Gps
{
    public class GpsTrace : PolylineItem
    {
        public string? Name { get; set; }
        
        public List<DateTime> TimeStamps { get; } = [];

        public Location? Center => Locations is null || Locations.Count == 0 ? null : new Location(
            Locations.Select(l => l.Latitude).Sum() / Locations.Count,
            Locations.Select(l => l.Longitude).Sum() / Locations.Count);
       
        public static GpsTrace[] DecodeGpsTraceFile(string fileName, TimeSpan minimumInterval)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            using var file = File.OpenRead(fileName);
            if (ext == ".gpx")
                return GpxDecoder.DecodeStream(file).ToArray();
            if (ext == ".kml")
                return KmlDecoder.DecodeStream(file, minimumInterval).ToArray();
            throw new FileFormatException("Unsupported file format");
        }
    }
}
