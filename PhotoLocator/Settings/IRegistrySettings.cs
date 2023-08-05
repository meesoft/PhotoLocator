namespace PhotoLocator.Settings
{
    interface IRegistrySettings : ISettings
    {
        int FirstLaunch { get; set; }
        int LeftColumnWidth { get; set; }
        string PhotoFolderPath { get; set; }
        string RenameMasks { get; set; }
        string? SelectedLayer { get; set; }
        ViewMode ViewMode { get; set; }

        object? GetValue(string name);
        void SetValue(string name, object value);
    }
}