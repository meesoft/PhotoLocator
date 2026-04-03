using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PhotoLocator.Helpers
{
    public class ElementToNegativePositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var result = new TranslateTransform();
            var geometry = value as FrameworkElement;
            if (geometry != null)
            {
                var pos = geometry.TransformToVisual(Application.Current.MainWindow);
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
