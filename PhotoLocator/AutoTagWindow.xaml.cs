using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
