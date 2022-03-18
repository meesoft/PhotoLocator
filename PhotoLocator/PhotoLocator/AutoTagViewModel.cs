using MapControl;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    class AutoTagViewModel : INotifyPropertyChanged
    {
        public AutoTagViewModel(IEnumerable<PictureItemViewModel> pictures, IEnumerable<GpsTrace> polylines, Action completedAction)
        {
            Pictures = pictures;
            GpsTraces = polylines;
            CompletedAction = completedAction;
            var settings = new RegistrySettings();
            _traceFilePath = settings.Key.GetValue(nameof(TraceFilePath)) as string;
            _maxTimestampDifference = (settings.Key.GetValue(nameof(MaxTimestampDifference)) as int? ?? 15 * 60) / 60.0;
            _timestampOffset = (settings.Key.GetValue(nameof(TimestampOffset)) as int? ?? 0) / 3600.0;
        }

        public IEnumerable<PictureItemViewModel> Pictures { get; }

        public IEnumerable<GpsTrace> GpsTraces { get; }

        public string? TraceFilePath { get => _traceFilePath; set => SetProperty(ref _traceFilePath, value); }
        private string? _traceFilePath;

        /// <summary>
        /// In minutes
        /// </summary>
        public double MaxTimestampDifference { get => _maxTimestampDifference; set => SetProperty(ref _maxTimestampDifference, value); }
        private double _maxTimestampDifference;

        /// <summary>
        /// In hours
        /// </summary>
        public double TimestampOffset { get => _timestampOffset; set => SetProperty(ref _timestampOffset, value); }
        private double _timestampOffset;

        public Action CompletedAction { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }

            return false;
        }

        public ICommand OkCommand => new RelayCommand(o =>
        {
            var gpsTraces = LoadAdditionalGpsTraces();
            var result = AutoTag(gpsTraces);
            CompletedAction();
            SaveSettings();
            MessageBox.Show($"{result.Tagged} photos with timestamps were tagged, {result.NotTagged} were not.", "Auto tag", MessageBoxButton.OK, MessageBoxImage.Information);
        });


        private Location? GetBestGeoFix(PictureItemViewModel[] sourceImages, IEnumerable<GpsTrace> gpsTraces, DateTime timeStamp)
        {
            Location? bestFix = null;
            var minDist = TimeSpan.FromMinutes(MaxTimestampDifference + 0.001);
            // Search in other images
            foreach (var geoFix in sourceImages)
            {
                var dist = (timeStamp - geoFix.TimeStamp!.Value).Duration();
                if (dist < minDist)
                {
                    minDist = dist;
                    bestFix = geoFix.GeoTag;
                }
            }
            // Search in GPS traces
            timeStamp += TimeSpan.FromHours(TimestampOffset);
            foreach (var trace in gpsTraces)
                for (int i = 0; i < trace.TimeStamps.Count; i++)
                {
                    var dist = (timeStamp - trace.TimeStamps[i]).Duration();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestFix = trace.Locations![i];
                    }
                }
            return bestFix;
        }

        private (int Tagged, int NotTagged) AutoTag(IEnumerable<GpsTrace> gpsTraces)
        {
            int tagged = 0, notTagged = 0;
            var sourceImages = Pictures.Where(i => i.GeoTag != null && i.TimeStamp.HasValue).ToArray();
            foreach (var item in Pictures.Where(i => i.IsSelected && i.GeoTag is null && i.TimeStamp.HasValue))
            {
                var bestFix = GetBestGeoFix(sourceImages, gpsTraces, item.TimeStamp!.Value);
                if (bestFix != null)
                {
                    item.GeoTag = bestFix;
                    item.GeoTagSaved = false;
                    tagged++;
                }
                else
                    notTagged++;
            }
            return (tagged, notTagged);
        }
        
        private IEnumerable<GpsTrace> LoadAdditionalGpsTraces()
        {
            if (string.IsNullOrEmpty(TraceFilePath))
                return GpsTraces;
            if (File.Exists(TraceFilePath))
                return GpsTraces.Append(GpsTrace.DecodeGpxFile(TraceFilePath));
            return GpsTraces.Concat(
                Directory.EnumerateFiles(TraceFilePath, "*.gpx").Select(fileName => GpsTrace.DecodeGpxFile(fileName)));
        }

        private void SaveSettings()
        {
            var settings = new RegistrySettings();
            settings.Key.SetValue(nameof(TraceFilePath), TraceFilePath ?? String.Empty);
            settings.Key.SetValue(nameof(MaxTimestampDifference), IntMath.Round(MaxTimestampDifference * 60));
            settings.Key.SetValue(nameof(TimestampOffset), IntMath.Round(TimestampOffset * 3600));
        }
    }
}
