namespace PhotoLocator
{
    interface IRegistrySettings
    {
        int FirstLaunch { get; set; }
        int LeftColumnWidth { get; set; }
        string PhotoFileExtensions { get; set; }
        string PhotoFolderPath { get; set; }
        string RenameMasks { get; set; }
        string SavedFilePostfix { get; set; }
        string? SelectedLayer { get; set; }
        bool ShowFolders { get; set; }
        bool ShowMetadataInSlideShow { get; set; }
        int SlideShowInterval { get; set; }
        ViewMode ViewMode { get; set; }

        object? GetValue(string name);
        void SetValue(string name, object value);
    }
}