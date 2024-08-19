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
                var text = new StringBuilder();
                using var licenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.LICENSE")!, Encoding.Latin1);
                text.AppendLine(licenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("Third party open source licenses:");
                text.AppendLine();
                text.AppendLine("____");
                using var mapLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.XamlMapControlLicense")!, Encoding.Latin1);
                text.AppendLine(mapLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("____");
                using var apiLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.Windows-API-Code-PackLicense")!, Encoding.Latin1);
                text.AppendLine(apiLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("____");
                using var psdLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.PsdLicense")!, Encoding.Latin1);
                text.AppendLine(psdLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("____");
                using var contextMenuLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.ExplorerShellContextMenuLicense.txt")!, Encoding.Latin1);
                text.AppendLine(contextMenuLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("____");
                using var jpegTransformLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.JpegTransformLicense.txt")!, Encoding.Latin1);
                text.AppendLine(jpegTransformLicenseStream.ReadToEnd());

                return text.ToString();
            }
        }

        public ICommand CheckForUpdatesCommand => new RelayCommand(o =>
        {
            using var cursor = new MouseCursorOverride();
            var version = GetType().Assembly.GetName().Version!;
            var url = $"http://meesoft.com/PhotoLocator/CheckForUpdates.php?Version={(version.Major % 100):D3}{version.Minor:D3}{version.Build:D3}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        });
    }
}
