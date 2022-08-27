using Microsoft.VisualBasic.FileIO;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for RenameWindow.xaml
    /// </summary>
    public sealed partial class RenameWindow : Window, INotifyPropertyChanged, IDisposable
    {
        readonly IList<PictureItemViewModel> _selectedPictures;
        readonly ObservableCollection<PictureItemViewModel> _allPictures;
        readonly string[] _previousMasks;
        MaskBasedNaming? _exampleNamer;

#if DEBUG
        public RenameWindow() : this(new List<PictureItemViewModel>(), new ObservableCollection<PictureItemViewModel>())
        {
            RenameMask = nameof(RenameMask);
            ExampleName = nameof(ExampleName);
            IsExtensionWarningVisible = true;
        }
#endif

        public RenameWindow(IList<PictureItemViewModel> selectedPictures, ObservableCollection<PictureItemViewModel> allPictures)
        {
            InitializeComponent();
            Title = $"Rename {selectedPictures.Count} file(s)";
            _selectedPictures = selectedPictures;
            _allPictures = allPictures;
            _renameMask = string.Empty;

            using var settings = new RegistrySettings();
            _previousMasks = settings.RenameMasks.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            MaskMenuButton.ContextMenu = new ContextMenu();
            foreach (var mask in _previousMasks)
            {
                var menuItem = new MenuItem { Header = mask.Replace("_", "__", StringComparison.Ordinal), Tag = mask };
                menuItem.Click += HandleMaskItemClick;
                MaskMenuButton.ContextMenu.Items.Add(menuItem);
            }
            if (selectedPictures.Count == 1 || _previousMasks.Length == 0)
                RenameMask = selectedPictures[0].Name;
            else
                RenameMask = _previousMasks[0];
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            MaskTextBox.CaretIndex = RenameMask.Length;
            MaskTextBox.Focus();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string RenameMask
        {
            get => _renameMask;
            set 
            {
                if (SetProperty(ref _renameMask, value))
                {
                    try
                    {
                        if (_exampleNamer is null)
                            _exampleNamer = new MaskBasedNaming(_selectedPictures[0], 0);
                        ExampleName = _exampleNamer.GetFileName(RenameMask);
                        ErrorMessage = null;
                        IsExtensionWarningVisible = !Path.GetExtension(ExampleName).Equals(
                            Path.GetExtension(_exampleNamer.OriginalFileName), StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        IsExtensionWarningVisible = false;
                        ExampleName = null;
                        ErrorMessage = ex.Message;
                    }
                }
            }
        }
        private string _renameMask;

        public string? ExampleName
        {
            get => _exampleName;
            set => SetProperty(ref _exampleName, value);
        }
        private string? _exampleName;

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    NotifyPropertyChanged(nameof(IsErrorVisible));
                    NotifyPropertyChanged(nameof(IsMaskValid));
                }
            }
        }
        private string? _errorMessage;

        public bool IsMaskValid => ErrorMessage is null;

        public bool IsErrorVisible => !IsMaskValid;

        public bool IsExtensionWarningVisible
        {
            get => _isExtensionWarningVisible;
            set => SetProperty(ref _isExtensionWarningVisible, value);
        }
        private bool _isExtensionWarningVisible;

        private void MaskMenuButtonClick(object sender, RoutedEventArgs e)
        {
            MaskMenuButton.ContextMenu.IsOpen = true;
        }

        private void HandleMaskItemClick(object sender, RoutedEventArgs e)
        {
            RenameMask = (string)((MenuItem)sender).Tag;
        }

        private void HandleEscapeCodeTextBlockMouseUp(object sender, MouseButtonEventArgs e)
        {
            var tag = ((TextBlock)sender).Text;
            tag = tag[0..(tag.IndexOf('|', 1) + 1)];
            var carretIndex = MaskTextBox.CaretIndex;
            MaskTextBox.Text = MaskTextBox.Text.Insert(carretIndex, tag);
            MaskTextBox.CaretIndex = carretIndex + tag.Length;
        }

        private void HandleRenameButtonClick(object sender, RoutedEventArgs e)
        {
            if (_selectedPictures.Count > 1 && !RenameMask.Contains('|'))
                throw new UserMessageException("Multiple files cannot be renamed to the same name.");
            RenameMask = RenameMask.Trim();
            _exampleNamer?.Dispose();
            _exampleNamer = null;
            try
            {
                int counter = 0;
                foreach (var item in _selectedPictures)
                {
                    string newName;
                    using (var namer = new MaskBasedNaming(item, counter++))
                        newName = namer.GetFileName(RenameMask);

                    // Allow overwriting when renaming single picture
                    if (_selectedPictures.Count == 1 && item.IsFile)
                    {
                        var overwritingFile = _allPictures.FirstOrDefault(f => f != item && f.IsFile && f.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                        if (overwritingFile != null &&
                            MessageBox.Show($"The file {newName} already exists, do you want to overwrite it?", "Rename", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                            {
                                overwritingFile.Recycle();
                                _allPictures.Remove(overwritingFile);
                            }
                    }

                    item.Rename(newName, Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName));
                    _allPictures.Remove(item);
                    item.InsertOrdered(_allPictures);
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (RenameMask.Contains('|', StringComparison.Ordinal))
            {
                using var settings = new RegistrySettings();
                settings.RenameMasks = string.Join('\\', (new[] { RenameMask }).Concat(_previousMasks).Distinct().Take(10));
            }
            DialogResult = true;
        }

        public void Dispose()
        {
            _exampleNamer?.Dispose();
        }
    }
}
