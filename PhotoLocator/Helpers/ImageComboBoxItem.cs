using System.Windows.Media;

namespace PhotoLocator.Helpers;

public class ImageComboBoxItem
{
    public ImageSource? Image { get; set; }

    public string? Text { get; set; }

    public object? Tag { get; set; }

    public override string? ToString() => Text ?? base.ToString();
}
