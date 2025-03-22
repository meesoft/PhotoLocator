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

        public PictureItemViewModel(string fileName, bool isDirectory, PropertyChangedEventHandler handleFilePropertyChanged, ISettings? settings)
        {
            _name = Path.GetFileName(fileName);
            _fullPath = fileName;
            IsDirectory = isDirectory;
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
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        bool _isSelected;

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
        bool _isChecked;

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

        public DateTime? TimeStamp
        {
            get => _timeStamp;
            set => SetProperty(ref _timeStamp, value);
        }
        DateTime? _timeStamp;

        public ImageSource? ThumbnailImage
        {
            get => _thumbnailImage;
            set => SetProperty(ref _thumbnailImage, value);
        }
        ImageSource? _thumbnailImage;

        public int ThumbnailSize => _settings?.ThumbnailSize ?? 256;

        public string? MetadataString
        {
            get
            {
                if (_metadataString is null && IsFile)
                {
                    var metadataString = GetMetadataString();
                    _metadataString ??= metadataString;
                }
                return _metadataString;
            }
            set => SetProperty(ref _metadataString, value);
        }
        string? _metadataString;

        private string GetMetadataString()
        {
            try
            {
                return ExifHandler.GetMetadataString(FullPath);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
                return string.Empty;
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        string? _errorMessage;

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

        public Rotation Rotation { get; set; }

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
                            () => _screenDpi = VisualTreeHelper.GetDpi(App.Current.MainWindow));
                    var thumbnailPixelSize = ThumbnailSize * _screenDpi.DpiScaleX;
                    int thumbnailIntPixelSize = IntMath.Round(thumbnailPixelSize);
                    thumbnail = LoadPreviewInternal(thumbnailIntPixelSize, false, ct);
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
                catch
                {
                    return TryLoadShellThumbnail(large: false, ShellThumbnailFormatOption.IconOnly, ct);
                }
            }, ct);
        }

        private async Task LoadMetadataAsync(CancellationToken ct)
        {
            try
            {
                if (IsVideo)
                    return;
                GeoTag = await Task.Run(async () =>
                {
                    using var file = await FileHelpers.OpenFileWithRetryAsync(FullPath, ct);
                    var metadata = ExifHandler.LoadMetadata(file);
                    if (metadata is null)
                        return null;
                    var orientation = metadata.GetQuery(ExifHandler.OrientationQuery1) as ushort? ?? metadata.GetQuery(ExifHandler.OrientationQuery2) as ushort? ?? 0;
                    Rotation = orientation switch
                    {
                        3 => Rotation.Rotate180,
                        6 => Rotation.Rotate90,
                        8 => Rotation.Rotate270,
                        _ => Rotation.Rotate0
                    };
                    _timeStamp = ExifHandler.GetTimeStamp(metadata);
                    _metadataString ??= ExifHandler.GetMetadataString(metadata);
                    return ExifHandler.GetGeotag(metadata);
                }, ct);
                GeoTagSaved = GeoTag != null;
            }
            catch (NotSupportedException)
            {
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
        }

        public BitmapSource? LoadPreview(CancellationToken ct, int maxWidth = int.MaxValue, bool preservePixelFormat = false)
        {
            try
            {
                Log.Write("Loading preview of " + Name);
                return LoadPreviewInternal(maxWidth, preservePixelFormat, ct);
            }
            catch (OperationCanceledException)
            {
                Log.Write("Cancelled loading preview of " + Name);
                throw;
            }
            catch
            {
                Log.Write("Loading thumbnail of " + Name);
                return TryLoadShellThumbnail(large: true, ShellThumbnailFormatOption.Default, ct);
            }
        }

        private BitmapSource LoadPreviewInternal(int maxPixelWidth, bool preservePixelFormat, CancellationToken ct)
        {
            if (IsVideo && !string.IsNullOrEmpty(_settings?.FFmpegPath))
            {
                try
                {
                    var (result, timestamp, location, metadata) = VideoFileFormatHandler.LoadFromFile(FullPath, maxPixelWidth, _settings, ct);
                    if (metadata is not null)
                        MetadataString = metadata;
                    if (location is not null && GeoTag is null)
                    {
                        GeoTag = location;
                        GeoTagSaved = true;
                    }
                    if (timestamp.HasValue && !TimeStamp.HasValue)
                        TimeStamp = timestamp.Value;
                    return result;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }
            using var fileStream = File.OpenRead(FullPath);
            try
            {
                var ext = Path.GetExtension(Name).ToLowerInvariant();
                if (CR2FileFormatHandler.CanLoad(ext))
                    return CR2FileFormatHandler.LoadFromStream(fileStream, Rotation, maxPixelWidth, preservePixelFormat, ct);
                if (CR3FileFormatHandler.CanLoad(ext))
                    return CR3FileFormatHandler.LoadFromStream(fileStream, Rotation, maxPixelWidth, preservePixelFormat, ct);
                if (PhotoshopFileFormatHandler.CanLoad(ext))
                    return PhotoshopFileFormatHandler.LoadFromStream(fileStream, Rotation, maxPixelWidth, preservePixelFormat, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                fileStream.Position = 0; // Fallback to default reader
            }
            ct.ThrowIfCancellationRequested();
            return GeneralFileFormatHandler.LoadFromStream(fileStream, Rotation, maxPixelWidth, preservePixelFormat, ct);
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
                    ErrorMessage = ex.ToString();
                return null;
            }
        }

        internal async Task SaveGeoTagAsync(CancellationToken ct)
        {
            try
            {
                await ExifHandler.SetGeotagAsync(FullPath, GetProcessedFileName(),
                    GeoTag ?? throw new InvalidOperationException(nameof(GeoTag) + " not set"),
                    _settings?.ExifToolPath, ct);
                GeoTagSaved = true;
            }
            catch (UserMessageException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
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
