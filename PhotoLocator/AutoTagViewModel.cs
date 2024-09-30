using MapControl;
using PhotoLocator.Gps;
using PhotoLocator.Helpers;
using PhotoLocator.Settings;
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
        readonly IEnumerable<PictureItemViewModel> _allItems;
        readonly IEnumerable<PictureItemViewModel> _selectedItems;
        readonly IRegistrySettings _settings;

        public AutoTagViewModel(IEnumerable<PictureItemViewModel> allItems, IEnumerable<PictureItemViewModel> selectedItems, IEnumerable<GpsTrace> polylines, 
            Action completedAction, IRegistrySettings settings)
        {
            _allItems = allItems;
            _selectedItems = selectedItems;
            GpsTraces = polylines;
            CompletedAction = completedAction;
            _settings = settings;
            _traceFilePath = _settings.GetValue(nameof(TraceFilePath)) as string;
            _maxTimestampDifference = (_settings.GetValue(nameof(MaxTimestampDifference)) as int? ?? 15 * 60) / 60.0;
            _timestampOffset = (_settings.GetValue(nameof(TimestampOffset)) as int? ?? 0) / 3600.0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public bool IsWindowEnabled { get => _isWindowEnabled; set => SetProperty(ref _isWindowEnabled, value); }
        private bool _isWindowEnabled = true;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public ICommand OkCommand => new RelayCommand(o =>
        {
            IsWindowEnabled = false;
            try
            {
                using var _ = new MouseCursorOverride();
                var gpsTraces = LoadAdditionalGpsTraces().ToArray();
                var (tagged, notTagged) = AutoTag(gpsTraces);
                if (MessageBox.Show($"{tagged} photos with timestamps were tagged, {notTagged} were not.", "Auto tag", 
                    MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
                    return;
                CompletedAction();
                SaveSettings();
            }
            finally
            {
                IsWindowEnabled = true; 
            }
        });

        private Location? GetBestGeoFix(PictureItemViewModel[] sourceImages, IEnumerable<GpsTrace> gpsTraces, DateTime timeStamp)
        {
            Location? bestFix = null;
            var minDistance = TimeSpan.FromMinutes(MaxTimestampDifference + 0.001);
            // Search in other images
            foreach (var geoFix in sourceImages)
            {
                var distance = (timeStamp - geoFix.TimeStamp!.Value).Duration();
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestFix = geoFix.GeoTag;
                }
            }
            // Search in GPS traces
            timeStamp += TimeSpan.FromHours(TimestampOffset);
            foreach (var trace in gpsTraces)
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

        internal (int Tagged, int NotTagged) AutoTag(IEnumerable<GpsTrace> gpsTraces)
        {
            int tagged = 0, notTagged = 0;
            var sourceImages = _allItems.Where(item => item.GeoTag != null && item.TimeStamp.HasValue && !_selectedItems.Contains(item)).ToArray();
            foreach (var item in _selectedItems.Where(item => item.TimeStamp.HasValue && item.CanSaveGeoTag))
            {
                var bestTag = GetBestGeoFix(sourceImages, gpsTraces, item.TimeStamp!.Value);
                if (bestTag != null)
                {
                    if (!Equals(item.GeoTag, bestTag))
                    {
                        item.GeoTag = bestTag;
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
        
        private IEnumerable<GpsTrace> LoadAdditionalGpsTraces()
        {
            if (string.IsNullOrEmpty(TraceFilePath))
                return GpsTraces;

            // Support Windows Copy Full Path string to be used by the user, Example: "C:\temp\file.jpeg"
            if (TraceFilePath.Contains("\"")) TraceFilePath = TraceFilePath.Replace("\"", "");

            var minDistance = TimeSpan.FromMinutes(MaxTimestampDifference);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(_selectedItems.First().FullPath)!);
            if (File.Exists(TraceFilePath))
                return GpsTraces.Concat(GpsTrace.DecodeGpsTraceFile(TraceFilePath, minDistance));
            return GpsTraces.
                Concat(Directory.EnumerateFiles(TraceFilePath, "*.gpx").SelectMany(fileName => GpsTrace.DecodeGpsTraceFile(fileName, minDistance))).
                Concat(Directory.EnumerateFiles(TraceFilePath, "*.kml").SelectMany(fileName => GpsTrace.DecodeGpsTraceFile(fileName, minDistance)));
        }

        private void SaveSettings()
        {
            _settings.SetValue(nameof(TraceFilePath), TraceFilePath ?? String.Empty);
            _settings.SetValue(nameof(MaxTimestampDifference), IntMath.Round(MaxTimestampDifference * 60));
            _settings.SetValue(nameof(TimestampOffset), IntMath.Round(TimestampOffset * 3600));
        }
    }
}
