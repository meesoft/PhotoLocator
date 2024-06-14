using System;
using System.Globalization;
#if WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
#elif UWP
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
#else
using System.Windows;
using System.Windows.Data;
#endif

namespace PhotoLocator.MapDisplay
{
    public class HeadingToVisibilityConverter : IValueConverter
    {
        public static object Convert(object value, Type targetType, object parameter, string language)
        {
            return (double)value != 0d ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, string.Empty);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ConvertBack(value, targetType, parameter, string.Empty);
        }
    }
}