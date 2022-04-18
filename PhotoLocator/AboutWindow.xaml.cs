using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

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
                using var popimsLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.LICENSE")!);
                text.AppendLine(popimsLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("Third party open source licenses:");
                text.AppendLine();
                text.AppendLine("____");
                using var mapLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.XamlMapControlLicense")!);
                text.AppendLine(mapLicenseStream.ReadToEnd());
                text.AppendLine();
                text.AppendLine("____");
                using var apiLicenseStream = new StreamReader(typeof(AboutWindow).Assembly.GetManifestResourceStream(
                    "PhotoLocator.Resources.Windows-API-Code-PackLicense")!);
                text.AppendLine(apiLicenseStream.ReadToEnd());

                return text.ToString();
            }
        }
    }
}
