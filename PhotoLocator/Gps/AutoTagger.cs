using MapControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoLocator.Gps
{
    class AutoTagger
    {
        readonly IEnumerable<PictureItemViewModel> _allItems;
        readonly IEnumerable<GpsTrace> _gpsTraces;
        readonly TimeSpan _timestampOffset;
        readonly double _maxTimestampDifference;

        public AutoTagger(IEnumerable<PictureItemViewModel> allItems, IEnumerable<GpsTrace> gpsTraces, TimeSpan timestampOffset, double maxTimestampDifference)
        {
            _allItems = allItems;
            _gpsTraces = gpsTraces;
            _timestampOffset = timestampOffset;
            _maxTimestampDifference = maxTimestampDifference;
        }

        public (int Tagged, int NotTagged) AutoTag(IEnumerable<PictureItemViewModel> selectedItems)
        {
            int tagged = 0, notTagged = 0;
            var sourceImages = _allItems.Where(item => item.GeoTagPresent && item.TimeStamp.HasValue && !selectedItems.Contains(item)).ToArray();
            foreach (var item in selectedItems.Where(item => item.TimeStamp.HasValue && item.CanSaveGeoTag))
            {
                var bestTag = GetBestGeoFix(sourceImages, item.TimeStamp!.Value);
                if (bestTag != null)
                {
                    if (!Equals(item.Location, bestTag))
                    {
                        item.Location = bestTag;
                        item.GeoTagSaved = false;
                        item.IsChecked = false;
                    }
                    tagged++;
                }
                else
                    notTagged++;
            }
            return (tagged, notTagged);
        }

        private Location? GetBestGeoFix(PictureItemViewModel[] sourceImages, DateTimeOffset timeStamp)
        {
            Location? bestFix = null;
            var minDistance = TimeSpan.FromMinutes(_maxTimestampDifference + 0.001);
            // Search in other images
            foreach (var geoFix in sourceImages)
            {
                var distance = (timeStamp - geoFix.TimeStamp!.Value).Duration();
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestFix = geoFix.Location;
                }
            }
            // Search in GPS traces
            timeStamp += _timestampOffset;
            foreach (var trace in _gpsTraces)
                for (int i = 0; i < trace.TimeStamps.Count; i++)
                {
                    var distance = (timeStamp - trace.TimeStamps[i]).Duration();
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestFix = trace.Locations![i];
                    }
                }
            return bestFix;
        }
    }
}
