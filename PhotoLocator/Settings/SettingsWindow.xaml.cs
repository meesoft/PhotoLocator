using PhotoLocator.Helpers;
using System;
using System.IO;
using System.Windows;

namespace PhotoLocator.Settings
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public ObservableSettings Settings { get; } = new ObservableSettings();

        private void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            OkButton.Focus();

            if (!string.IsNullOrEmpty(Settings.SavedFilePostfix))
            { 
                foreach(var ch in Path.GetInvalidFileNameChars())
                    if (Settings.SavedFilePostfix.Contains(ch, StringComparison.Ordinal))
                        throw new UserMessageException($"Postfix contains invalid character '{ch}'");
            }

            if (!string.IsNullOrEmpty(Settings.ExifToolPath))
            {
                const string ExifToolName = "exiftool.exe";

                if (!File.Exists(Settings.ExifToolPath))
                {
                    var newPath = Path.Combine(Settings.ExifToolPath, ExifToolName);
                    if (File.Exists(newPath))
                        Settings.ExifToolPath = newPath;
                    else
                        throw new UserMessageException(ExifToolName + " not found in specified path");
                }
                else if (Settings.ExifToolPath.EndsWith("(-k).exe", StringComparison.OrdinalIgnoreCase))
                    throw new UserMessageException($"Invalid ExifTool executable name (-k means pause before exiting)");
            }
            if (!string.IsNullOrEmpty(Settings.FFmpegPath))
            {
                const string FFmpegToolName = "ffmpeg.exe";

                if (!File.Exists(Settings.FFmpegPath))
                {
                    var newPath = Path.Combine(Settings.FFmpegPath, FFmpegToolName);
                    if (File.Exists(newPath))
                        Settings.FFmpegPath = newPath;
                    else
                        throw new UserMessageException(FFmpegToolName + " not found in specified path");
                }
            }

            DialogResult = true;
        }
    }
}