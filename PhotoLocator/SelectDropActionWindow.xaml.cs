using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary> Interaction logic for SelectDropActionWindow.xaml </summary>
    public partial class SelectDropActionWindow : Window, INotifyPropertyChanged
    {
        readonly MainViewModel _mainViewModel;

        public SelectDropActionWindow(string[] droppedEntries, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            Title += $" {droppedEntries.Length} files";
            DroppedEntries = droppedEntries;
            DataContext = this;
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
        }

        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

        public IList<string> DroppedEntries { get; }

        public string? CurrentPath => _mainViewModel.PhotoFolderPath;

        public bool IsIncludeAvailable => DroppedEntries.Any(f => _mainViewModel.Items.All(i => i.FullPath != f));

        public bool IsCopyAndMoveAvailable => !string.IsNullOrEmpty(CurrentPath) && Path.GetDirectoryName(DroppedEntries[0]) != CurrentPath;

        public ICommand IncludeCommand => new RelayCommand(async o =>
        {
            DialogResult = true;
            if (DroppedEntries.All(f => _mainViewModel.Items.Any(i => i.FullPath == f)))
                return;
            await _mainViewModel.WaitForPicturesLoadedAsync();
            if (DroppedEntries.Any(f => Path.GetDirectoryName(f) != CurrentPath))
                _mainViewModel.PhotoFolderPath = null;
            var fileNames = new List<string>();
            foreach (var path in DroppedEntries)
                if (Directory.Exists(path))
                    await _mainViewModel.AppendFilesAsync(Directory.EnumerateFiles(path));
                else
                    fileNames.Add(path);
            if (fileNames.Count > 0)
            {
                await _mainViewModel.AppendFilesAsync(fileNames);
                var firstDropped = _mainViewModel.Items.FirstOrDefault(item => item.FullPath == fileNames[0]);
                _mainViewModel.SelectIfNotNull(firstDropped);
            }
            await _mainViewModel.LoadPicturesAsync();
        });

        public ICommand SelectCommand => new RelayCommand(async o =>
        {
            DialogResult = true;
            if (Directory.Exists(DroppedEntries[0]))
            {
                await _mainViewModel.SetFolderPathAsync(DroppedEntries[0]);
                return;
            }
            var firstFile = DroppedEntries.FirstOrDefault(f => File.Exists(f));
            if (firstFile is null)
                return;
            var path = Path.GetDirectoryName(firstFile);
            if (path == CurrentPath)
            {
                var selectItem = _mainViewModel.Items.FirstOrDefault(item => item.FullPath == firstFile);
                _mainViewModel.SelectIfNotNull(selectItem);

            }
            else
                await _mainViewModel.SetFolderPathAsync(path, firstFile);
        });

        public ICommand CopyCommand => new RelayCommand(async o =>
        {
            DialogResult = true;
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                for (int i = 0; i < DroppedEntries.Count; i++)
                {
                    await Task.Run(() =>
                    {
                        var destinationPath = Path.Combine(CurrentPath!, Path.GetFileName(DroppedEntries[i]));
                        if (Directory.Exists(DroppedEntries[i]))
                            FileSystem.CopyDirectory(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        else
                            FileSystem.CopyFile(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                    }, ct);
                    progressCallback((double)(i + 1) / DroppedEntries.Count);
                }
                await SelectFirstDroppedAsync();
            }, "Copying...");
        });

        public ICommand MoveCommand => new RelayCommand(async o =>
        {
            DialogResult = true;
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                for (int i = 0; i < DroppedEntries.Count; i++)
                {
                    await Task.Run(() =>
                    {
                        var destinationPath = Path.Combine(CurrentPath!, Path.GetFileName(DroppedEntries[i]));
                        if (Directory.Exists(DroppedEntries[i]))
                            FileSystem.MoveDirectory(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        else
                            FileSystem.MoveFile(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                    }, ct);
                    progressCallback((double)(i + 1) / DroppedEntries.Count);
                }
                await SelectFirstDroppedAsync();
            }, "Moving...");
        });

        private async Task SelectFirstDroppedAsync()
        {
            var firstDropped = Path.Combine(CurrentPath!, Path.GetFileName(DroppedEntries[0]));
            if (Directory.Exists(firstDropped))
                await _mainViewModel.AddOrUpdateItemAsync(firstDropped, true, true);
            else if (File.Exists(firstDropped))
                await _mainViewModel.AddOrUpdateItemAsync(firstDropped, false, true);
        }
    }
}
