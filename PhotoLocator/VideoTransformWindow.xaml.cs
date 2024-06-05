using System.Windows;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for VideoTransformWindow.xaml
    /// </summary>
    public partial class VideoTransformWindow : Window
    {
        public VideoTransformWindow()
        {
            InitializeComponent();
        }

        private void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
