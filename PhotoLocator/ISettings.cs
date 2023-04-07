namespace PhotoLocator
{
    public interface ISettings
    {
        string? SavedFilePostfix { get; }

        string? ExifToolPath { get; }
    }
}
