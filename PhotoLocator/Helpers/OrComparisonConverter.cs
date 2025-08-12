using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PhotoLocator.Helpers
{
    public class OrComparisonConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var value = values[0];
            foreach(var p in values.Skip(1))
                if (p.Equals(value))
                    return true;
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
