using System.Windows;

namespace PhotoLocator.Helpers
{
    public static class DropDownSliderService
    {
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached(
            "Minimum", typeof(double), typeof(DropDownSliderService), new PropertyMetadata(0.0));

        public static void SetMinimum(DependencyObject element, double value) => element.SetValue(MinimumProperty, value);
        public static double GetMinimum(DependencyObject element) => (double)element.GetValue(MinimumProperty);

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached(
            "Maximum", typeof(double), typeof(DropDownSliderService), new PropertyMetadata(100.0));

        public static void SetMaximum(DependencyObject element, double value) => element.SetValue(MaximumProperty, value);
        public static double GetMaximum(DependencyObject element) => (double)element.GetValue(MaximumProperty);
    }
}
