using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PhotoLocator.Helpers
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    class BooleanToHiddenVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool visible && visible ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }
}
