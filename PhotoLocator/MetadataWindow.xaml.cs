using System.Windows;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for MetadataWindow.xaml
    /// </summary>
    public partial class MetadataWindow : Window
    {
        public MetadataWindow()
        {
            InitializeComponent();
            MetadataTextBox.Focus();
        }

        public string? Metadata { get; internal set; }
    }
}
