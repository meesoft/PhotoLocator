using Microsoft.Win32;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    public class JpegTransformCommands
    {
        private readonly IMainViewModel _mainViewModel;

        private bool HasFileSelected(object? o) => _mainViewModel.SelectedItem is not null && _mainViewModel.SelectedItem.IsFile;

        public JpegTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public ICommand RotateLeftCommand => new RelayCommand(async o => await RotateSelectedAsync(270), HasFileSelected);

        public ICommand RotateRightCommand => new RelayCommand(async o => await RotateSelectedAsync(90), HasFileSelected);

        public ICommand Rotate180Command => new RelayCommand(async o => await RotateSelectedAsync(180), HasFileSelected);

        private async Task RotateSelectedAsync(int angle)
        {
            var allSelected = _mainViewModel.GetSelectedItems(true).Where(item => JpegTransformations.IsFileTypeSupported(item.Name)).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("Unsupported file format");
            await _mainViewModel.RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                progressCallback(-1);
                int i = 0;
                foreach (var item in allSelected)
                {
                    JpegTransformations.Rotate(item.FullPath, item.GetProcessedFileName(), angle); //TODO: Make async
                    item.Orientation = Rotation.Rotate0;
                    item.IsChecked = false;
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Rotating...");
        }

        public async Task CropSelectedItemAsync(BitmapSource pictureSource, Rect cropRectangle)
        {
            var SelectedItem = _mainViewModel.SelectedItem!;
            string sourceFileName, targetFileName;
            if (JpegTransformations.IsFileTypeSupported(SelectedItem.Name))
            {
                sourceFileName = SelectedItem.FullPath;
                targetFileName = SelectedItem.GetProcessedFileName();
                SelectedItem.Orientation = Rotation.Rotate0;
            }
            else
            {
                sourceFileName = targetFileName = Path.ChangeExtension(SelectedItem.GetProcessedFileName(), "jpg");
                if (File.Exists(sourceFileName) && MessageBox.Show($"Do you wish to overwrite the file '{Path.GetFileName(sourceFileName)}'?", "Crop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                    return;
            }
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                await using var pause = _mainViewModel.PauseFileSystemWatcher();
                if (sourceFileName != SelectedItem.FullPath)
                {
                    using var file = await FileHelpers.OpenFileWithRetryAsync(SelectedItem.FullPath, ct);
                    await Task.Run(() =>
                    {
                        BitmapMetadata? metadata = null;
                        try
                        {
                            metadata = ExifHandler.LoadMetadata(file);
                        }
                        catch { } // Ignore if there is no supported metadata
                        GeneralFileFormatHandler.SaveToFile(pictureSource, sourceFileName, metadata, _mainViewModel.Settings.JpegQuality);
                    }, ct);
                }
                await Task.Run(() => JpegTransformations.Crop(sourceFileName, targetFileName, cropRectangle), ct);
                await _mainViewModel.AddOrUpdateItemAsync(targetFileName, false, true);
            }, "Cropping");
        }

        public ICommand LocalContrastCommand => new RelayCommand(async o =>
        {
            LocalContrastViewModel localContrastViewModel;
            BitmapMetadata? metadata;
            var allSelected = _mainViewModel.GetSelectedItems(true).ToArray();
            var selectedItem = _mainViewModel.SelectedItem!;
            using (var cursor = new MouseCursorOverride())
            {
                (var image, metadata) = await Task.Run(() => LoadImageWithMetadata(selectedItem));
                localContrastViewModel = new LocalContrastViewModel() { SourceBitmap = image };
            }
            var window = new LocalContrastView();
            window.Owner = Application.Current.MainWindow;
            window.OkButton.Content = "_Save as...";
            window.DataContext = localContrastViewModel;
            try
            {
                if (window.ShowDialog() != true)
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

        private static (BitmapSource, BitmapMetadata?) LoadImageWithMetadata(PictureItemViewModel item)
        {
            BitmapMetadata? metadata = null;
            var image = item.LoadPreview(default, int.MaxValue, true);
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
            if (metadata is null && item.GeoTag is not null)
            {
                metadata = new BitmapMetadata("jpg");
                ExifHandler.SetDateTaken(metadata, item.TimeStamp ?? File.GetLastWriteTime(item.FullPath));
                ExifHandler.SetGeotag(metadata, item.GeoTag);
            }
            return (image, metadata);
        }

        private async Task SaveProcessedImageAsync(LocalContrastViewModel localContrastViewModel, BitmapMetadata metadata, PictureItemViewModel selectedItem)
        {
            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(selectedItem.FullPath);
            dlg.FileName = Path.GetFileNameWithoutExtension(selectedItem.Name) + ".jpg";
            dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
            dlg.DefaultExt = "jpg";
            if (dlg.ShowDialog() != true)
                return;
            using (new MouseCursorOverride(Cursors.AppStarting))
            {
                var sameDir = Path.GetDirectoryName(selectedItem.FullPath) == Path.GetDirectoryName(dlg.FileName);
                await Task.Run(() => GeneralFileFormatHandler.SaveToFile(localContrastViewModel.PreviewPictureSource!, dlg.FileName, metadata, _mainViewModel.Settings.JpegQuality));
                if (sameDir)
                    await _mainViewModel.AddOrUpdateItemAsync(dlg.FileName, false, false);
            }
        }

        private async Task BatchProcessLocalContrastAsync(LocalContrastViewModel localContrastViewModel, BitmapMetadata metadata, PictureItemViewModel[] allSelected, PictureItemViewModel selectedItem)
        {
            await _mainViewModel.RunProcessWithProgressBarAsync((progressCallback, ct) => Task.Run(() =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    var targetFileName = Path.ChangeExtension(item.GetProcessedFileName(), "jpg");
                    if (item == selectedItem)
                        GeneralFileFormatHandler.SaveToFile(localContrastViewModel.PreviewPictureSource!, targetFileName, metadata, _mainViewModel.Settings.JpegQuality);
                    else
                    {
                        var (image, itemMetadata) = LoadImageWithMetadata(item);
                        image = localContrastViewModel.ApplyOperations(image);
                        GeneralFileFormatHandler.SaveToFile(image, targetFileName, itemMetadata, _mainViewModel.Settings.JpegQuality);
                    }
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }, ct), "Batch process");
        }
    }
}
