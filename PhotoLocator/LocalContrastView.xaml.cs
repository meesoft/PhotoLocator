using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for LocalContrastView.xaml
    /// </summary>
    public partial class LocalContrastView : Window
    {
        public LocalContrastView()
        {
            InitializeComponent();
        }

        LocalContrastViewModel ViewModel => (LocalContrastViewModel)DataContext;

        private void HandleOriginalButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.PreviewPictureSource = ViewModel.SourceBitmap;
        }

        private async void HandleOriginalButtonMouseUp(object sender, MouseButtonEventArgs e)
        {
            await ViewModel.UpdatePreviewAsync();
        }

        private async void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            if (DialogResult is not null)
                return;
            await ViewModel.FinishPreviewAsync();
            DialogResult ??= true;
        }
    }
}
