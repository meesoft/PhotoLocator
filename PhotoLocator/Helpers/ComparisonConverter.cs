﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace PhotoLocator.Helpers
{
    public class ComparisonConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.Equals(true) is true ? parameter : Binding.DoNothing;
        }
    }
}
