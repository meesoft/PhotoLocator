﻿using Microsoft.Win32;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
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
                    item.Rotation = Rotation.Rotate0;
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
                SelectedItem.Rotation = Rotation.Rotate0;
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
                if (sourceFileName != SelectedItem.FullPath)
                {
                    using var pause = _mainViewModel.PauseFileSystemWatcher();
                    using var file = await FileHelpers.OpenFileWithRetryAsync(SelectedItem.FullPath, ct);
                    await Task.Run(() => GeneralFileFormatHandler.SaveToFile(pictureSource, sourceFileName, ExifHandler.LoadMetadata(file), _mainViewModel.Settings.JpegQuality), ct);
                }
                await Task.Run(() => JpegTransformations.Crop(sourceFileName, targetFileName, cropRectangle), ct);
                _mainViewModel.AddOrUpdateItem(targetFileName, false, true);
            }, "Cropping");
        }

        public ICommand LocalContrastCommand => new RelayCommand(async o =>
        {
            LocalContrastViewModel localContrastViewModel;
            BitmapMetadata? metadata = null;
            using (var cursor = new MouseCursorOverride())
            {
                var image = _mainViewModel.SelectedItem!.LoadPreview(default, int.MaxValue, true);
                try
                {
                    using var file = File.OpenRead(_mainViewModel.SelectedItem.FullPath);
                    var decoder = BitmapDecoder.Create(file, ExifHandler.CreateOptions, BitmapCacheOption.OnLoad);
                    metadata = decoder.Frames[0].Metadata as BitmapMetadata;
                    image ??= decoder.Frames[0];
                }
                catch { }
                if (metadata is null && _mainViewModel.SelectedItem.GeoTag is not null)
                {
                    metadata = new BitmapMetadata("jpg");
                    ExifHandler.SetDateTaken(metadata, _mainViewModel.SelectedItem.TimeStamp ?? File.GetLastWriteTime(_mainViewModel.SelectedItem.FullPath));
                    ExifHandler.SetGeotag(metadata, _mainViewModel.SelectedItem.GeoTag);
                }
                localContrastViewModel = new LocalContrastViewModel()
                {
                    SourceBitmap = image ?? throw new UserMessageException(_mainViewModel.SelectedItem.ErrorMessage)
                };
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
            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(_mainViewModel.SelectedItem.FullPath);
            dlg.FileName = Path.GetFileNameWithoutExtension(_mainViewModel.SelectedItem.Name) + ".jpg";
            dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
            dlg.DefaultExt = "jpg";
            if (dlg.ShowDialog() != true)
                return;
            using (new MouseCursorOverride(Cursors.AppStarting))
            {
                var sameDir = Path.GetDirectoryName(_mainViewModel.SelectedItem.FullPath) == Path.GetDirectoryName(dlg.FileName);
                await Task.Run(() => GeneralFileFormatHandler.SaveToFile(localContrastViewModel.PreviewPictureSource!, dlg.FileName, metadata, _mainViewModel.Settings.JpegQuality));
                if (sameDir)
                    _mainViewModel.AddOrUpdateItem(dlg.FileName, false, false);
            }
        }, HasFileSelected);
    }
}
