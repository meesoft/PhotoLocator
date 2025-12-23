using MapControl;
using Microsoft.VisualBasic.FileIO;
using Microsoft.WindowsAPICodePack.Shell;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    [DebuggerDisplay("Name={Name}")]
    public class PictureItemViewModel : IFileInformation, INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#endif
        static DpiScale _screenDpi;
        readonly ISettings? _settings;

        public event PropertyChangedEventHandler? PropertyChanged;

#if DEBUG
        public PictureItemViewModel()
        {
            _name = nameof(PictureItemViewModel);
            if (_isInDesignMode)
            {
                _geoTag = new Location(0, 0);
                _geoTagSaved = true;
            }
        }
#endif

        public PictureItemViewModel(string fileName, bool isDirectory, PropertyChangedEventHandler? handleFilePropertyChanged, ISettings? settings)
        {
            _name = Path.GetFileName(fileName);
            _fullPath = fileName;
            IsDirectory = isDirectory;
            if (handleFilePropertyChanged is not null)
                PropertyChanged += handleFilePropertyChanged;
            _settings = settings;
        }

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }
        string _name;

        public string FullPath
        {
            get => _fullPath;
            private set => SetProperty(ref _fullPath, value);
        }
        string _fullPath = String.Empty;

        public bool IsDirectory { get; }

        public bool IsFile => !IsDirectory;

        public bool IsSelected
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool IsChecked
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool IsVideo
        {
            get
            {
                if (IsDirectory)
                    return false;
                var ext = Path.GetExtension(FullPath).ToLowerInvariant();
                var isVideo = ext is ".mp4" or ".mov" or ".avi";
                return isVideo;
            }
        }

        public bool GeoTagSaved
        {
            get => _geoTagSaved;
            set
            {
                if (SetProperty(ref _geoTagSaved, value))
                    NotifyPropertyChanged(nameof(GeoTagUpdated));
            }
        }
        bool _geoTagSaved;

        public bool GeoTagUpdated
        {
            get => GeoTag != null && !GeoTagSaved;
        }

        public bool GeoTagPresent
        {
            get => GeoTag != null;
        }

        public Location? GeoTag
        {
            get => _geoTag;
            set
            {
                if (SetProperty(ref _geoTag, value))
                {
                    NotifyPropertyChanged(nameof(GeoTagUpdated));
                    NotifyPropertyChanged(nameof(GeoTagPresent));
                }
            }
        }
        Location? _geoTag;

        public DateTimeOffset? TimeStamp
        {
            get => _timeStamp;
            set => SetProperty(ref _timeStamp, value);
        }
        DateTimeOffset? _timeStamp;

        public ImageSource? ThumbnailImage
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int ThumbnailSize => _settings?.ThumbnailSize ?? 256;

        public string? MetadataString
        {
            get
            {
                if (_metadataString is null && IsFile)
                    Task.Run(() => LoadMetadataAsync(default)).GetAwaiter().GetResult();
                return _metadataString;
            }
            set => SetProperty(ref _metadataString, value);
        }
        string? _metadataString;

        public string? ErrorMessage
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool CanSaveGeoTag
        {
            get
            {
                if (IsDirectory)
                    return false;
                if (_settings != null && !string.IsNullOrEmpty(_settings.ExifToolPath))
                    return true;
                var ext = Path.GetExtension(Name)?.ToLowerInvariant();
                return ext is ".jpg" or ".jpeg";
            }
        }

        public Rotation Orientation { get; set; }

        public async ValueTask LoadThumbnailAndMetadataAsync(CancellationToken ct)
        {
            if (IsFile)
                await LoadMetadataAsync(ct);
            if (ThumbnailImage is not null)
                return;
            ThumbnailImage = await Task.Run(() =>
            {
                var thumbnail = TryLoadShellThumbnail(large: false, IsFile ? ShellThumbnailFormatOption.ThumbnailOnly : ShellThumbnailFormatOption.Default, ct);
                if (thumbnail is not null || IsDirectory)
                    return thumbnail;
                try
                {
                    if (_screenDpi.DpiScaleX == 0)
                        App.Current.Dispatcher.Invoke(
                            () => _screenDpi = _screenDpi = VisualTreeHelper.GetDpi(App.Current.MainWindow));
                    var thumbnailPixelSize = ThumbnailSize * _screenDpi.DpiScaleX;
                    int thumbnailIntPixelSize = IntMath.Round(thumbnailPixelSize);
                    thumbnail = LoadPreviewInternal(thumbnailIntPixelSize, false, null, ct);
                    if (thumbnail.PixelWidth <= thumbnailIntPixelSize && thumbnail.PixelHeight <= thumbnailIntPixelSize)
                        return thumbnail;
                    ct.ThrowIfCancellationRequested();
                    var scale = Math.Min(thumbnailPixelSize / thumbnail.PixelWidth, thumbnailPixelSize / thumbnail.PixelHeight);
                    thumbnail = new TransformedBitmap(thumbnail, new ScaleTransform(scale, scale));
                    thumbnail.Freeze();
                    return thumbnail;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Write($"Failed to loads thumbnail for {Name}: {ex}");
                    return TryLoadShellThumbnail(large: false, ShellThumbnailFormatOption.IconOnly, ct);
                }
            }, ct);
        }

        private async Task LoadMetadataAsync(CancellationToken ct)
        {
            try
            {
                if (_settings is not null && _settings.ForceUseExifTool && !string.IsNullOrEmpty(_settings.ExifToolPath))
                    (GeoTag, _timeStamp, _metadataString, Orientation) = await Task.Run(() => ExifTool.DecodeMetadata(FullPath, _settings.ExifToolPath), ct);
                else
                    (GeoTag, _timeStamp, _metadataString, Orientation) = await Task.Run(() => ExifHandler.DecodeMetadataAsync(FullPath, IsVideo, _settings?.ExifToolPath, ct), ct);
                GeoTagSaved = GeoTag != null;
            }
            catch (NotSupportedException) { }
            catch (Exception ex)
            {
                Log.Write($"Failed to load metadata for {Name}: {ex}");
                ErrorMessage = ex.Message;
            }
        }

        public BitmapSource? LoadPreview(CancellationToken ct, int maxWidth = int.MaxValue, bool preservePixelFormat = false, string? skipTo = null)
        {
            try
            {
                Log.Write("Loading preview of " + Name);
                var sw = Stopwatch.StartNew();
                var preview = LoadPreviewInternal(maxWidth, preservePixelFormat, skipTo, ct);
                Log.Write($"Loaded preview of {Name} in {sw.ElapsedMilliseconds} ms");
                return preview;
            }
            catch (OperationCanceledException)
            {
                Log.Write("Cancelled loading preview of " + Name);
                throw;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(ex);
                Log.Write("Loading thumbnail of " + Name);
                return TryLoadShellThumbnail(large: true, ShellThumbnailFormatOption.Default, ct);
            }
        }

        private BitmapSource LoadPreviewInternal(int maxPixelWidth, bool preservePixelFormat, string? skipTo, CancellationToken ct)
        {
            if (IsVideo && !string.IsNullOrEmpty(_settings?.FFmpegPath))
            {
                try
                {
                    var (result, timestamp, location, metadata) = VideoFileFormatHandler.LoadFromFile(FullPath, maxPixelWidth, skipTo, _settings, ct);
                    if (metadata is not null && string.IsNullOrEmpty(MetadataString))
                        MetadataString = metadata;
                    if (location is not null && GeoTag is null)
                    {
                        GeoTag = location;
                        GeoTagSaved = true;
                    }
                    if (timestamp.HasValue && !TimeStamp.HasValue)
                        _timeStamp = timestamp.Value;
                    return result;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch { } // Ignore errors and fallback to default reader
            }
            using var fileStream = File.OpenRead(FullPath);
            try
            {
                var ext = Path.GetExtension(Name).ToLowerInvariant();
                if (CR2FileFormatHandler.CanLoad(ext))
                    return CR2FileFormatHandler.LoadFromStream(fileStream, Orientation, maxPixelWidth, preservePixelFormat, ct);
                if (CR3FileFormatHandler.CanLoad(ext))
                    return CR3FileFormatHandler.LoadFromStream(fileStream, Orientation, maxPixelWidth, preservePixelFormat, ct);
                if (PhotoshopFileFormatHandler.CanLoad(ext))
                    return PhotoshopFileFormatHandler.LoadFromStream(fileStream, Orientation, maxPixelWidth, preservePixelFormat, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(ex);
                fileStream.Position = 0; // Fallback to default reader
            }
            ct.ThrowIfCancellationRequested();
            return GeneralFileFormatHandler.LoadFromStream(fileStream, Orientation, maxPixelWidth, preservePixelFormat, ct);
        }

        private BitmapSource? TryLoadShellThumbnail(bool large, ShellThumbnailFormatOption formatOption, CancellationToken ct)
        {
            try
            {
                using var shellFile = IsDirectory ? ShellFolder.FromParsingName(FullPath) : ShellFile.FromFilePath(FullPath);
                shellFile.Thumbnail.FormatOption = formatOption;
                var thumbnail = large ? shellFile.Thumbnail.ExtraLargeBitmapSource : shellFile.Thumbnail.BitmapSource;
                ct.ThrowIfCancellationRequested();
                thumbnail.Freeze();
                ct.ThrowIfCancellationRequested();
                return thumbnail;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;
                if (formatOption != ShellThumbnailFormatOption.ThumbnailOnly)
                    ErrorMessage = ex.Message;
                Log.Write($"Failed to loads thumbnail for {Name}: {ex}");
                return null;
            }
        }

        internal async Task SaveGeoTagAsync(CancellationToken ct)
        {
            try
            {
                await ExifTool.SetGeotagAsync(FullPath, GetProcessedFileName(),
                    GeoTag ?? throw new InvalidOperationException(nameof(GeoTag) + " not set"),
                    _settings?.ExifToolPath, ct);
                GeoTagSaved = true;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(ex);
                ErrorMessage = ex.Message;
            }
        }

        public string GetProcessedFileName()
        {
            var postfix = _settings?.SavedFilePostfix;
            if (string.IsNullOrEmpty(postfix))
                return FullPath;
            var baseName = Path.GetFileNameWithoutExtension(Name);
            if (baseName.EndsWith(postfix, StringComparison.Ordinal))
                return FullPath;
            return Path.Combine(Path.GetDirectoryName(FullPath)!, baseName) + postfix + Path.GetExtension(Name);
        }

        public async Task RenameAsync(string newName, string newFullPath, bool renameSidecar)
        {
            await Task.Run(() =>
            {
                if (IsDirectory)
                    Directory.Move(FullPath, newFullPath);
                else
                {
                    File.Move(FullPath, newFullPath);
                    if (renameSidecar)
                    {
                        try
                        {
                            foreach (var sidecar in GetSidecarFiles())
                            {
                                var sidecarName = Path.GetFileName(sidecar);
                                if (Path.GetFileNameWithoutExtension(sidecarName).Equals(Path.GetFileNameWithoutExtension(Name), StringComparison.OrdinalIgnoreCase))
                                    File.Move(sidecar, Path.Combine(Path.GetDirectoryName(sidecar)!, Path.ChangeExtension(newName, Path.GetExtension(sidecarName))));
                                else //  Sidecar name is full name with additional extension
                                    File.Move(sidecar, Path.Combine(Path.GetDirectoryName(sidecar)!, newName + sidecarName[Name.Length..]));
                            }
                        }
                        catch (Exception ex)
                        {
                            File.Move(newFullPath, FullPath); // Rename back if renaming sidecar fails
                            ExceptionHandler.LogException(ex);
                            throw new UserMessageException("Unable to rename sidecar file: " + ex.Message, ex);
                        }
                    }
                }
            });
            Name = newName;
            FullPath = newFullPath;
        }

        private IEnumerable<string> GetSidecarFiles()
        {
            return Directory.GetFiles(Path.GetDirectoryName(FullPath)!, Path.ChangeExtension(Name, "xmp")).Concat(
                Directory.GetFiles(Path.GetDirectoryName(FullPath)!, Name + ".*", System.IO.SearchOption.AllDirectories));
        }

        internal void Renamed(string newFullPath)
        {
            FullPath = newFullPath;
            Name = Path.GetFileName(newFullPath);
        }

        internal void CopyTo(string destination)
        {
            if (IsDirectory)
                throw new UserMessageException("Copying directories is not supported");
            File.Copy(FullPath, destination, true);
        }

        internal void MoveTo(string destination)
        {
            if (IsDirectory)
                Directory.Move(FullPath, destination);
            else
                File.Move(FullPath, destination, true);
        }

        public void Recycle(bool recycleSidecar)
        {
            if (IsDirectory)
                FileSystem.DeleteDirectory(FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else
            {
                FileSystem.DeleteFile(FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                if (recycleSidecar)
                {
                    foreach (var sidecar in GetSidecarFiles())
                        FileSystem.DeleteFile(sidecar, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }
        }
    }
}
