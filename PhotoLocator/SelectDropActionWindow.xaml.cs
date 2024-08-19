using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public IList<string> DroppedEntries { get; }

        public string? CurrentPath => _mainViewModel.PhotoFolderPath;

        public bool IsIncludeAvailable => DroppedEntries.Any(f => _mainViewModel.Items.All(i => i.FullPath != f));

        public bool IsCopyAndMoveAvailable => !string.IsNullOrEmpty(CurrentPath) && Path.GetDirectoryName(DroppedEntries[0]) != CurrentPath;

        public bool IsProgressBarVisible
        {
            get => _isProgressBarVisible;
            set => SetProperty(ref _isProgressBarVisible, value);
        }
        private bool _isProgressBarVisible;

        public double ProgressBarValue
        {
            get => _progressBarValue;
            set => SetProperty(ref _progressBarValue, value);
        }
        private double _progressBarValue;

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
                if (firstDropped != null)
                    _mainViewModel.SelectItem(firstDropped);
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
                if (selectItem != null)
                    _mainViewModel.SelectItem(selectItem);

            }
            else
                await _mainViewModel.SetFolderPathAsync(path, firstFile);
        });

        public ICommand CopyCommand => new RelayCommand(async o =>
        {
            try
            {
                IsProgressBarVisible = true;
                IsEnabled = false;
                for (int i = 0; i < DroppedEntries.Count; i++)
                    await Task.Run(() =>
                    {
                        var destinationPath = Path.Combine(CurrentPath!, Path.GetFileName(DroppedEntries[i]));
                        if (Directory.Exists(DroppedEntries[i]))
                            FileSystem.CopyDirectory(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        else
                            FileSystem.CopyFile(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        ProgressBarValue = (double)(i + 1) / DroppedEntries.Count;
                    });
                await SelectFirstDroppedAsync();
            }
            finally
            {
                DialogResult = true;
            }
        });

        public ICommand MoveCommand => new RelayCommand(async o =>
        {
            try
            {
                IsProgressBarVisible = true;
                IsEnabled = false;
                for (int i = 0; i < DroppedEntries.Count; i++)
                    await Task.Run(() =>
                    {
                        var destinationPath = Path.Combine(CurrentPath!, Path.GetFileName(DroppedEntries[i]));
                        if (Directory.Exists(DroppedEntries[i]))
                            FileSystem.MoveDirectory(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        else
                            FileSystem.MoveFile(DroppedEntries[i], destinationPath, UIOption.AllDialogs);
                        ProgressBarValue = (double)(i + 1) / DroppedEntries.Count;
                    });
                await SelectFirstDroppedAsync();
            }
            finally
            {
                DialogResult = true;
            }
        });

        private async Task SelectFirstDroppedAsync()
        {
            await _mainViewModel.WaitForFileSystemWatcherOperation();
            var firstDropped = Path.GetFileName(DroppedEntries.First());
            var selectItem = _mainViewModel.Items.FirstOrDefault(item => item.Name == firstDropped);
            if (selectItem != null)
                _mainViewModel.SelectItem(selectItem);
        }
    }
}
