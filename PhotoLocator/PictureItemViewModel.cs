using MapControl;
using Microsoft.VisualBasic.FileIO;
using Microsoft.WindowsAPICodePack.Shell;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    [DebuggerDisplay("Name={Name}")]
    public class PictureItemViewModel : INotifyPropertyChanged
    {
#if DEBUG
        static readonly bool _isInDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
#else
        const bool _isInDesignMode = false;
#endif
        private bool _updateThumbnail;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PictureItemViewModel()
        {
            _name = nameof(PictureItemViewModel);
#if DEBUG
            if (_isInDesignMode)
            {
                _geoTag = new Location(0, 0);
                _geoTagSaved = true;
            }
#endif
        }

        public PictureItemViewModel(string fileName, bool isDirectory)
        {
            _name = Path.GetFileName(fileName);
            FullPath = fileName;
            IsDirectory = isDirectory;
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
            set => SetProperty(ref _name, value);
        }
        string _name;

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
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

        public string? ErrorMessage 
        { 
            get => _errorMessage; 
            set => SetProperty(ref _errorMessage, value); 
        }
        string? _errorMessage;

        public bool CanSaveGeoTag => IsFile && Path.GetExtension(Name)?.ToLowerInvariant() == ".jpg";

        public Rotation Rotation { get => _rotation; set => _rotation = value; }
        Rotation _rotation;

        public async ValueTask LoadMetadataAndThumbnailAsync(CancellationToken ct)
        {
            if (IsFile)
                await LoadMetadata(ct);
            if (ThumbnailImage != null)
                return;
            ThumbnailImage = await Task.Run(() =>
            {
                var thumbnail = LoadShellThumbnail(large: false, IsFile ? ShellThumbnailFormatOption.ThumbnailOnly : ShellThumbnailFormatOption.Default, ct);
                _updateThumbnail = thumbnail is null;
                if (_updateThumbnail)
                    return LoadShellThumbnail(large: false, ShellThumbnailFormatOption.IconOnly, ct);
                return thumbnail;
            }, ct);
        }

        private async Task LoadMetadata(CancellationToken ct)
        {
            try
            {
                GeoTag = await Task.Run(() =>
                {
                    using var file = File.OpenRead(FullPath);
                    var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    if (decoder.Frames[0].Metadata is not BitmapMetadata metadata)
                        return null;
                    var orientation = metadata.GetQuery(ExifHandler.OrientationQuery1) as ushort? ?? metadata.GetQuery(ExifHandler.OrientationQuery2) as ushort? ?? 0;
                    Rotation = orientation switch
                    {
                        3 => Rotation.Rotate180,
                        6 => Rotation.Rotate90,
                        8 => Rotation.Rotate270,
                        _ => Rotation.Rotate0
                    };
                    if (DateTime.TryParse(metadata.DateTaken, out var dateTaken))
                        _timeStamp = DateTime.SpecifyKind(dateTaken, DateTimeKind.Local);
                    return ExifHandler.GetGeotag(metadata);
                }, ct);
                GeoTagSaved = GeoTag != null;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
        }

        public BitmapSource? LoadPreview(CancellationToken ct, int maxWidth = int.MaxValue)
        {
            try
            {
                var preview = LoadPreviewInternal(maxWidth, ct);
                if (preview is not null && (_updateThumbnail || ThumbnailImage is null))
                {
                    _updateThumbnail = false;
                    if (preview.PixelWidth > 256 || preview.PixelHeight > 256)
                    {
                        var scale = Math.Min(256.0 / preview.PixelWidth, 256.0 / preview.PixelHeight);
                        var thumbnail = new TransformedBitmap(preview, new ScaleTransform(scale, scale));
                        thumbnail.Freeze();
                        ThumbnailImage = thumbnail;
                    }
                    else
                        ThumbnailImage = preview;
                }
                return preview;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;
                return LoadShellThumbnail(large: true, ShellThumbnailFormatOption.Default, ct);
            }
        }

        private BitmapSource? LoadPreviewInternal(int maxWidth, CancellationToken ct)
        {
            using var fileStream = File.OpenRead(FullPath);
            try
            {
                var ext = Path.GetExtension(Name).ToLowerInvariant();
                BitmapSource? result = null;
                if (CR2FileFormatHandler.CanLoad(ext) && (result = CR2FileFormatHandler.TryLoadFromStream(fileStream, Rotation, maxWidth, ct)) != null)
                    return result;
                if (CR3FileFormatHandler.CanLoad(ext) && (result = CR3FileFormatHandler.TryLoadFromStream(fileStream, Rotation, maxWidth, ct)) != null)
                    return result;
            }
            catch
            {
                fileStream.Position = 0; // Fallback to default reader
            }
            ct.ThrowIfCancellationRequested();
            return GeneralFileFormatHandler.TryLoadFromStream(fileStream, Rotation, maxWidth, ct);
        }

        private BitmapSource? LoadShellThumbnail(bool large, ShellThumbnailFormatOption formatOption, CancellationToken ct)
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

        internal async Task SaveGeoTagAsync(string? postfix)
        {
            try
            {
                await Task.Run(() =>
                {
                    var newFileName = string.IsNullOrEmpty(postfix) ? FullPath :
                        Path.Combine(Path.GetDirectoryName(FullPath)!, Path.GetFileNameWithoutExtension(FullPath)) + postfix + Path.GetExtension(FullPath);
                    ExifHandler.SetGeotag(FullPath, newFileName, GeoTag ?? throw new InvalidOperationException(nameof(GeoTag) + " not set"));
                });
                GeoTagSaved = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.ToString();
            }
        }

        public void Rename(string newName, string newFullPath)
        {
            if (IsDirectory)
                Directory.Move(FullPath, newFullPath);
            else
                File.Move(FullPath, newFullPath);
            Name = newName;
            FullPath = newFullPath;
        }

        internal void Renamed(string newFullPath)
        {
            FullPath = newFullPath;
            Name = Path.GetFileName(newFullPath);
        }

        internal void MoveTo(string destination)
        {
            if (IsDirectory)
                Directory.Move(FullPath, destination);
            else
                File.Move(FullPath, destination);
        }

        public void Recycle()
        {
            if (IsDirectory)
                FileSystem.DeleteDirectory(FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else
                FileSystem.DeleteFile(FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
    }
}
