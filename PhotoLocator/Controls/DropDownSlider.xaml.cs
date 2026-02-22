using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PhotoLocator.Controls;

public partial class DropDownSlider : UserControl
{
    bool _onTextChangedRunning, _onNumericValueChangedRunning;

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
        if (d is not DropDownSlider control || control._onNumericValueChangedRunning)
            return;
        if (double.TryParse(control.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            control._onTextChangedRunning = true;
            control.NumericValue = Math.Clamp(v, control.Minimum, control.Maximum);
            control._onTextChangedRunning = false;
        }
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(DropDownSlider), new PropertyMetadata(0.0));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(DropDownSlider), new PropertyMetadata(100.0));
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency), typeof(double), typeof(DropDownSlider), new PropertyMetadata(1.0));
    public double TickFrequency { get => (double)GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }

    public static readonly DependencyProperty NumericValueProperty = DependencyProperty.Register(
        nameof(NumericValue), typeof(double), typeof(DropDownSlider), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnNumericValueChanged));
    public double NumericValue { get => (double)GetValue(NumericValueProperty); set => SetValue(NumericValueProperty, value); }

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(DropDownSlider), new PropertyMetadata(1, OnFormatChanged));
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    public static readonly DependencyProperty SliderValueUnitsProperty = DependencyProperty.Register(
        nameof(SliderValueUnits), typeof(string), typeof(DropDownSlider), new PropertyMetadata(string.Empty, OnFormatChanged));
    public string SliderValueUnits { get => (string)GetValue(SliderValueUnitsProperty); set => SetValue(SliderValueUnitsProperty, value); }

    static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownSlider control)
            control.UpdateFormattedValue();
    }

    public static readonly DependencyProperty FormattedNumericValueProperty = DependencyProperty.Register(
        nameof(FormattedNumericValue), typeof(string), typeof(DropDownSlider), new PropertyMetadata(string.Empty));
    public string FormattedNumericValue { get => (string)GetValue(FormattedNumericValueProperty); private set => SetValue(FormattedNumericValueProperty, value); }

    static void OnNumericValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DropDownSlider control)
            return;
        control._onNumericValueChangedRunning = true;
        if (!control._onTextChangedRunning)
            control.Text = control.NumericValue.ToString("F" + control.Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        control.UpdateFormattedValue();
        control._onNumericValueChangedRunning = false;
    }

    void UpdateFormattedValue()
    {
        FormattedNumericValue = NumericValue.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture) + SliderValueUnits;
    }
}
