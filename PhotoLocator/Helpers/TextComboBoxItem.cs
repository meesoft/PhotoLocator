namespace PhotoLocator.Helpers;

public class TextComboBoxItem
{
    public required string Text { get; set; }

    public object? Tag { get; set; }

    public override string ToString() => Text;
}
