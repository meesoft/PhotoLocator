using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using System;
using System.Collections.Generic;
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
    public sealed partial class RenameWindow : Window, INotifyPropertyChanged
    {
        readonly IList<PictureItemViewModel> _selectedPictures;
        readonly string[] _previousMasks;
        MaskBasedNaming? _exampleNamer;

#if DEBUG
        public RenameWindow() : this(new List<PictureItemViewModel>())
        {
            RenameMask = nameof(RenameMask);
            ExampleName = nameof(ExampleName);
            IsExtensionWarningVisible = true;
        }
#endif

        public RenameWindow(IList<PictureItemViewModel> selectedPictures)
        {
            InitializeComponent();
            Title = $"Rename {selectedPictures.Count} file(s)";
            _selectedPictures = selectedPictures;
            _renameMask = string.Empty;

            using var settings = new RegistrySettings();
            _previousMasks = settings.RenameMasks.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            MaskMenuButton.ContextMenu = new ContextMenu();
            foreach (var mask in _previousMasks)
            {
                var menuItem = new MenuItem { Header = mask };
                menuItem.Click += HandleMaskItemClick;
                MaskMenuButton.ContextMenu.Items.Add(menuItem);
            }
            if (selectedPictures.Count == 1 || _previousMasks.Length == 0)
                RenameMask = selectedPictures[0].Name;
            else
                RenameMask = _previousMasks[0];
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
                            Path.GetExtension(_exampleNamer.OriginalFileName), StringComparison.InvariantCultureIgnoreCase);
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
            RenameMask = (string)((MenuItem)sender).Header;
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
                    var newFullPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
                    item.Rename(newName, newFullPath);
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (RenameMask.Contains('|'))
            {
                using var settings = new RegistrySettings();
                settings.RenameMasks = string.Join('\\', (new[] { RenameMask }).Concat(_previousMasks).Distinct().Take(10));
            }
            DialogResult = true;
        }
    }
}
