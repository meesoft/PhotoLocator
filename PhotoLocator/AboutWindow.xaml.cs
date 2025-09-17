using PhotoLocator.Helpers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string AboutText
        {
            get
            {
                var text = new StringBuilder();
                var assembly = GetType().Assembly;
                var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                text.AppendLine(versionInfo.FileDescription + " " + versionInfo.FileVersion);
                text.AppendLine(versionInfo.LegalCopyright);
                text.AppendLine(versionInfo.CompanyName);
                return text.ToString();
            }
        }

        public static string LicenseText
        {
            get
            {
                var separator = new string('_', 100);
                var text = new StringBuilder();
                using var licenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.LICENSE")!, Encoding.Latin1);
                text.AppendLine(licenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("Third party open source licenses:");
                text.AppendLine();

                text.AppendLine(separator);
                using var mapLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.XamlMapControlLicense")!, Encoding.Latin1);
                text.AppendLine(mapLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                using var apiLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.Windows-API-Code-PackLicense")!, Encoding.Latin1);
                text.AppendLine(apiLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                using var psdLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.PsdLicense")!, Encoding.Latin1);
                text.AppendLine(psdLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                using var contextMenuLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.ExplorerShellContextMenuLicense.txt")!, Encoding.Latin1);
                text.AppendLine(contextMenuLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                using var jpegTransformLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.JpegTransformLicense.txt")!, Encoding.Latin1);
                text.AppendLine(jpegTransformLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                text.AppendLine("ExifTool by Phil Harvey is licensed under the Artistic License");
                using var articticLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.ArtisticLicense.txt")!, Encoding.Latin1);
                text.AppendLine(articticLicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                text.AppendLine("shimat/opencvsharp and OpenCV is licensed under Apache License 2.0");
                using var apache2LicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.Apache2License.txt")!, Encoding.Latin1);
                text.AppendLine(apache2LicenseStream.ReadToEnd());
                text.AppendLine();

                text.AppendLine(separator);
                text.AppendLine("ffmpeg binaries are licensed under GPLv3");
                using var gpl3LicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.GPLv3License.txt")!, Encoding.Latin1);
                text.AppendLine(gpl3LicenseStream.ReadToEnd());

                text.AppendLine(separator);
                text.AppendLine("jpegli is licensed under the BSD 3-Clause license");
                using var jpegxlLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.JPEG-XL-LICENSE.txt")!, Encoding.Latin1);
                text.AppendLine(jpegxlLicenseStream.ReadToEnd());

                text.AppendLine(separator);
                text.AppendLine("SunCalcNet");
                using var sunCalcNetLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.SunCalcNetLicense.txt")!, Encoding.Latin1);
                text.AppendLine(sunCalcNetLicenseStream.ReadToEnd());                

                return text.ToString();
            }
        }

        public static ICommand OpenWebsiteCommand => new RelayCommand(o =>
        {
            using var cursor = new MouseCursorOverride();
            var url = $"https://meesoft.com/PhotoLocator";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        });

        public ICommand CheckForUpdatesCommand => new RelayCommand(o =>
        {
            using var cursor = new MouseCursorOverride();
            var version = GetType().Assembly.GetName().Version!;
            var url = $"https://meesoft.com/PhotoLocator/CheckForUpdates.php?Version={(version.Major % 100):D3}{version.Minor:D3}{version.Build:D3}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        });
    }
}
