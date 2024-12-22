using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for AutoTagWindow.xaml
    /// </summary>
    public partial class AutoTagWindow : Window
    {
        public AutoTagWindow()
        {
            InitializeComponent();
        }

        private void HandleTextBoxPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                OkButton.Focus(); // Make sure we leave the edit box so that the value is updated
        }
    }
}
