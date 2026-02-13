using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PhotoLocator.Controls;

public partial class DropDownSlider : UserControl
{
    public DropDownSlider()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(DropDownSlider), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownSlider control)
        {
            if (double.TryParse(control.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                // Clamp to range
                if (v < control.Minimum) v = control.Minimum;
                if (v > control.Maximum) v = control.Maximum;
                control.NumericValue = v;
            }
        }
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(DropDownSlider), new PropertyMetadata(0.0));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(DropDownSlider), new PropertyMetadata(100.0));
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public static readonly DependencyProperty NumericValueProperty = DependencyProperty.Register(
        nameof(NumericValue), typeof(double), typeof(DropDownSlider), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnNumericValueChanged));
    public double NumericValue { get => (double)GetValue(NumericValueProperty); set => SetValue(NumericValueProperty, value); }

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(DropDownSlider), new PropertyMetadata(1, OnDecimalsChanged));
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    static void OnDecimalsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownSlider control)
            control.UpdateFormattedValue();
    }

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency), typeof(double), typeof(DropDownSlider), new PropertyMetadata(0.1));
    public double TickFrequency { get => (double)GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }

    public static readonly DependencyProperty FormattedNumericValueProperty = DependencyProperty.Register(
        nameof(FormattedNumericValue), typeof(string), typeof(DropDownSlider), new PropertyMetadata(string.Empty));
    public string FormattedNumericValue { get => (string)GetValue(FormattedNumericValueProperty); set => SetValue(FormattedNumericValueProperty, value); }

    static void OnNumericValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownSlider control)
        {
            // Format with configured number of decimals
            control.Text = control.NumericValue.ToString("F" + control.Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            control.UpdateFormattedValue();
        }
    }

    void UpdateFormattedValue()
    {
        var fmt = "F" + Decimals.ToString(CultureInfo.InvariantCulture);
        FormattedNumericValue = NumericValue.ToString(fmt, CultureInfo.InvariantCulture);
    }
}
