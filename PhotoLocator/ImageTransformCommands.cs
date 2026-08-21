using Microsoft.Win32;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    public sealed class ImageTransformCommands
    {
        public const string AstroCommandParameter = "Astro";

        private readonly IMainViewModel _mainViewModel;

        private bool HasFileSelected(object? o) => _mainViewModel.SelectedItem is not null && _mainViewModel.SelectedItem.IsFile;

        public ImageTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public ICommand RotateLeftCommand => new RelayCommand(async o => await RotateSelectedAsync(270), HasFileSelected);

        public ICommand RotateRightCommand => new RelayCommand(async o => await RotateSelectedAsync(90), HasFileSelected);

        public ICommand Rotate180Command => new RelayCommand(async o => await RotateSelectedAsync(180), HasFileSelected);

        public ICommand Rotate0Command => new RelayCommand(async o =>
        { 
            if (MessageBox.Show("This will reset any rotation EXIF data from the selected images. Continue?", "Reset rotation tag", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            await RotateSelectedAsync(0); 
        }, HasFileSelected);

        private async Task RotateSelectedAsync(int angle)
        {
            var allSelected = _mainViewModel.GetSelectedItems(true).Where(item => JpegTransformations.IsFileTypeSupported(item.Name)).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("Unsupported file format");
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                int i = 0;
                foreach (var item in allSelected)
                {
                    await JpegTransformations.RotateAsync(item.FullPath, item.GetProcessedFileName(), angle, ct);
                    item.IsChecked = false;
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, "Rotating...");
        }

        enum CropMode { CropJpeg, CropOther, CreateJpeg }

        public async Task CropSelectedItemAsync(BitmapSource pictureSource, Rect cropRectangle)
        {
            var selectedItem = _mainViewModel.SelectedItem;
            if (selectedItem is null || !selectedItem.IsFile)
                return;

            CropMode mode;
            var sourceFileName = selectedItem.FullPath;
            var targetFileName = selectedItem.GetProcessedFileName();
            if (JpegTransformations.IsFileTypeSupported(selectedItem.Name))
                mode = CropMode.CropJpeg;
            else if (Path.GetExtension(selectedItem.Name).ToLowerInvariant() is ".tif" or ".tiff" or ".png" or ".bmp" or ".jxr")
                mode = CropMode.CropOther;
            else
            {
                sourceFileName = targetFileName = Path.ChangeExtension(targetFileName, "jpg");
                if (File.Exists(sourceFileName) && MessageBox.Show($"Do you wish to overwrite the file '{Path.GetFileName(sourceFileName)}'?", "Crop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                    return;
                mode = CropMode.CreateJpeg;
            }
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                await using var pause = _mainViewModel.PauseFileSystemWatcher();
                if (mode == CropMode.CropOther)
                {
                    var (sourceImage, metadata) = await LoadImageWithMetadataAsync(selectedItem);
                    var cropped = new FloatBitmap(sourceImage, 1).CopyRect((int)cropRectangle.Left, (int)cropRectangle.Top, (int)cropRectangle.Width, (int)cropRectangle.Height);
                    var use16bit = sourceImage.Format.BitsPerPixel is 16 or 48 or 96;
                    GeneralFileFormatHandler.SaveToFile(
                        use16bit ? cropped.ToBitmapSource16(sourceImage.DpiX, sourceImage.DpiY, 1) : cropped.ToBitmapSource(sourceImage.DpiX, sourceImage.DpiY, 1),
                        targetFileName, metadata, _mainViewModel.Settings);
                }
                if (mode == CropMode.CreateJpeg)
                {
                    using var file = await FileHelpers.OpenFileWithRetryAsync(selectedItem.FullPath, ct);
                    await Task.Run(() =>
                    {
                        BitmapMetadata? metadata = null;
                        try
                        {
                            metadata = ExifHandler.LoadMetadata(file);
                        }
                        catch { } // Ignore if there is no supported metadata
                        GeneralFileFormatHandler.SaveToFile(pictureSource, sourceFileName, metadata, _mainViewModel.Settings);
                    }, ct);
                }
                if (mode != CropMode.CropOther)
                    await JpegTransformations.CropAsync(sourceFileName, targetFileName, cropRectangle, ct);
                await _mainViewModel.AddOrUpdateItemAsync(targetFileName, false, true);
            }, "Cropping...");
        }

        public ICommand LocalContrastCommand => new RelayCommand(async o =>
        {
            LocalContrastViewModel localContrastViewModel;
            BitmapMetadata? metadata;
            var allSelected = _mainViewModel.GetSelectedItems(true).ToArray();
            var selectedItem = _mainViewModel.SelectedItem!;
            using (var cursor = new MouseCursorOverride())
            {
                (var image, metadata) = await Task.Run(() => LoadImageWithMetadataAsync(selectedItem));
                localContrastViewModel = new LocalContrastViewModel() 
                { 
                    IsAstroModeEnabled = o as string == AstroCommandParameter, 
                    SourceBitmap = image,
                    FileName = selectedItem.FullPath,
                };
                localContrastViewModel.TryLoadAdjustments();
            }
            var window = new LocalContrastView();
            window.Owner = Application.Current.MainWindow;
            window.OkButton.Content = "_Save as...";
            window.DataContext = localContrastViewModel;
            try
            {
                if (window.ShowDialog() is not true)
                    return;
            }
            finally
            {
                window.DataContext = null;
            }
            localContrastViewModel.SaveLastUsedValues();

            if (allSelected.Length > 1 &&
                MessageBox.Show($"Apply operation to all {allSelected.Length} selected files and save to JPG?",
                    "Batch process", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await BatchProcessLocalContrastAsync(localContrastViewModel, metadata, allSelected, selectedItem);
            else
                await SaveProcessedImageAsync(localContrastViewModel, metadata, selectedItem);
        }, HasFileSelected);

        private static async Task<(BitmapSource, BitmapMetadata?)> LoadImageWithMetadataAsync(PictureItemViewModel item)
        {
            BitmapMetadata? metadata = null;
            var image = await item.LoadPreviewAsync(default, preservePixelFormat: true);
            try
            {
                using var file = File.OpenRead(item.FullPath);
                var decoder = BitmapDecoder.Create(file, ExifHandler.CreateOptions, BitmapCacheOption.OnLoad);
                metadata = decoder.Frames[0].Metadata as BitmapMetadata;
                image ??= decoder.Frames[0];
            }
            catch (Exception ex)
            {
                if (image is null)
                    throw new UserMessageException(item.ErrorMessage ?? ex.Message, ex);
            }
            if (metadata is null && item.GeoTagPresent)
            {
                metadata = new BitmapMetadata("jpg");
                ExifHandler.SetDateTaken(metadata, item.TimeStamp ?? File.GetLastWriteTime(item.FullPath));
                ExifHandler.SetGeotag(metadata, item.Location);
                metadata.Freeze();
            }
            return (image, metadata);
        }

        private async Task SaveProcessedImageAsync(LocalContrastViewModel localContrastViewModel, BitmapMetadata? metadata, PictureItemViewModel selectedItem)
        {
            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(selectedItem.FullPath);
            dlg.FileName = Path.GetFileNameWithoutExtension(selectedItem.Name) + ".jpg";
            dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
            dlg.DefaultExt = "jpg";
            if (dlg.ShowDialog() is not true)
                return;
            using (new MouseCursorOverride(Cursors.AppStarting))
            {
                await using var pause = _mainViewModel.PauseFileSystemWatcher();
                var sameDir = Path.GetDirectoryName(selectedItem.FullPath) == Path.GetDirectoryName(dlg.FileName);
                await Task.Run(() =>
                {
                    var resultImage = localContrastViewModel.GetResultImage(
                        localContrastViewModel.SourceBitmap?.Format != PixelFormats.Cmyk32 &&
                        GeneralFileFormatHandler.ShouldProduce16bitOutputForFormat(dlg.FileName, _mainViewModel.Settings));
                    GeneralFileFormatHandler.SaveToFile(resultImage, dlg.FileName, ExifHandler.ResetOrientation(metadata), _mainViewModel.Settings);
                });
                if (sameDir)
                    await _mainViewModel.AddOrUpdateItemAsync(dlg.FileName, false, false);
            }
        }    

        private async Task BatchProcessLocalContrastAsync(LocalContrastViewModel localContrastViewModel, BitmapMetadata? metadata, PictureItemViewModel[] allSelected, PictureItemViewModel selectedItem)
        {
            await _mainViewModel.RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(async () =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    var targetFileName = Path.ChangeExtension(item.GetProcessedFileName(), "jpg");
                    if (item == selectedItem)
                    {
                        GeneralFileFormatHandler.SaveToFile(localContrastViewModel.PreviewPictureSource!, targetFileName,
                            ExifHandler.ResetOrientation(metadata), _mainViewModel.Settings);
                    }
                    else
                    {
                        var (image, itemMetadata) = await LoadImageWithMetadataAsync(item);
                        image = localContrastViewModel.ApplyOperations(image);
                        GeneralFileFormatHandler.SaveToFile(image, targetFileName,
                            ExifHandler.ResetOrientation(itemMetadata), _mainViewModel.Settings);
                    }
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Batch process");
        }

        public ICommand ConvertFileFormatCommand => new RelayCommand(async o =>
        {
            var allSelected = _mainViewModel.GetSelectedItems(true).ToArray();
            if (allSelected.Length == 0)
                return;
            var targetType = o as string ?? throw new ArgumentException("Invalid target type");

            var browser = new System.Windows.Forms.FolderBrowserDialog();
            browser.InitialDirectory = Path.GetDirectoryName(allSelected[0].FullPath)!;
            browser.Description = $"Select target folder for converted {targetType} files";
            browser.UseDescriptionForTitle = true;
            if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            var targetDir = browser.SelectedPath;
            var targetIsSourceDir = string.Equals(targetDir, browser.InitialDirectory, StringComparison.OrdinalIgnoreCase);

            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                var overwriteAll = false;
                int i = 0;
                foreach (var item in allSelected)
                {
                    var targetFileName = targetIsSourceDir ? Path.ChangeExtension(item.GetProcessedFileName(), targetType)
                        : Path.Combine(targetDir, Path.GetFileNameWithoutExtension(item.Name) + "." + targetType);
                    if (!overwriteAll && File.Exists(targetFileName))
                    {
                        if (MessageBox.Show(App.Current.MainWindow, $"File {targetFileName} already exists. Overwrite all conflicting files?",
                            "Confirm Overwrite All", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                            break;
                        overwriteAll = true;
                    }

                    if (targetType == "jxl" && Path.GetExtension(item.Name).ToLowerInvariant() is ".jpg" or ".jpeg")
                    {
                        await Task.Run(() => JpegXlFileFormatHandler.TranscodeToJxl(item.FullPath, targetFileName, null, ct), ct);
                    }
                    else
                    {
                        var (image, itemMetadata) = await LoadImageWithMetadataAsync(item);
                        await Task.Run(() => GeneralFileFormatHandler.SaveToFile(image, targetFileName,
                            ExifHandler.ResetOrientation(itemMetadata), _mainViewModel.Settings), ct);
                    }
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, "Convert to " + targetType);
        });
    }
}
