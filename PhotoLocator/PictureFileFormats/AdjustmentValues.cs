using System.IO;
using System.Text.Json;

namespace PhotoLocator.PictureFileFormats
{
    internal class ToneAdjustmentValues
    {
        public float AdjustHue { get; set; }
        public float AdjustSaturation { get; set; } = 1f;
        public float AdjustIntensity { get; set; } = 1f;
        public float HueUniformity { get; set; }
    }

    internal class AdjustmentValues
    {
        public double AstroStretch { get; set; }
        public double BackgroundRemovalSmooth { get; set; }
        public double BlackPoint { get; set; }
        public double HighlightStrength { get; set; }
        public double ShadowStrength { get; set; }
        public double OutlierReductionStrength { get; set; }
        public double Contrast { get; set; }
        public double ToneMapping { get; set; }
        public double DetailHandling { get; set; }
        public double MaxStretch { get; set; }
        public double ToneRotation { get; set; }
        public ToneAdjustmentValues[] ToneAdjustments { get; set; } = [];

        static JsonSerializerOptions? _saveOptions, _loadOptions;

        public void SaveToJsonFile(string path)
        {
            _saveOptions ??= new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, _saveOptions);
            File.WriteAllText(path, json);
        }

        public static AdjustmentValues LoadFromJsonFile(string path)
        {
            var json = File.ReadAllText(path);
            _loadOptions ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var av = JsonSerializer.Deserialize<AdjustmentValues>(json, _loadOptions);
            return av ?? throw new FileFormatException("Failed to deserialize adjustment values");
        }
    }
}
