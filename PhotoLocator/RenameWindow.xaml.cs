using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.Settings;
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
    public sealed partial class RenameWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private const int RenameHistoryLength = 20;

        readonly List<PictureItemViewModel> _selectedPictures;
        readonly OrderedCollection _allPictures;
        readonly PictureItemViewModel _focusedItem;
        readonly string[] _previousMasks;
        MaskBasedNaming? _exampleNamer;

#if DEBUG
        public RenameWindow() : this([], [], null!, new ObservableSettings())
        {
            RenameMask = nameof(RenameMask);
            ExampleName = nameof(ExampleName);
            IsExtensionWarningVisible = true;
        }
#endif

        public RenameWindow(IEnumerable<PictureItemViewModel> selectedPictures, OrderedCollection allPictures, PictureItemViewModel focusedItem, ISettings settings)
        {
            InitializeComponent();
            _selectedPictures = selectedPictures.ToList();
            Title = $"Rename {_selectedPictures.Count} file(s)";
            _allPictures = allPictures;
            _focusedItem = focusedItem;
            Settings = settings;
            _renameMask = string.Empty;

            using var registrySettings = new RegistrySettings();
            _previousMasks = registrySettings.RenameMasks.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            MaskMenuButton.ContextMenu = new ContextMenu();
            foreach (var mask in _previousMasks)
            {
                var menuItem = new MenuItem { Header = mask.Replace("_", "__", StringComparison.Ordinal), Tag = mask };
                menuItem.Click += HandleMaskItemClick;
                MaskMenuButton.ContextMenu.Items.Add(menuItem);
            }
            if (_selectedPictures.Count == 1 || _previousMasks.Length == 0)
                RenameMask = _selectedPictures[0].Name;
            else
                RenameMask = _previousMasks[0];
        }

        private void HandleWindowLoaded(object sender, RoutedEventArgs e)
        {
            MaskTextBox.CaretIndex = Math.Max(0, RenameMask.LastIndexOf('.'));
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

        public ISettings Settings { get; }

        public string RenameMask
        {
            get => _renameMask;
            set 
            {
                if (SetProperty(ref _renameMask, value))
                {
                    try
                    {
                        _exampleNamer ??= new MaskBasedNaming(_focusedItem, 0);
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
            get;
            set => SetProperty(ref field, value);
        }

        public string? ErrorMessage
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    NotifyPropertyChanged(nameof(IsErrorVisible));
                    NotifyPropertyChanged(nameof(IsMaskValid));
                }
            }
        }

        public bool IsMaskValid => ErrorMessage is null;

        public bool IsErrorVisible => !IsMaskValid;

        public bool IsExtensionWarningVisible
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool IsProgressBarVisible
        {
            get;
            set => SetProperty(ref field, value);
        }

        public double ProgressBarValue
        {
            get;
            set => SetProperty(ref field, value);
        }

        private void MaskMenuButtonClick(object sender, RoutedEventArgs e)
        {
            MaskMenuButton.ContextMenu.IsOpen = true;
        }

        private void HandleMaskItemClick(object sender, RoutedEventArgs e)
        {
            RenameMask = (string)((MenuItem)sender).Tag;
            MaskTextBox.Focus();
        }

        private void HandleEscapeCodeTextBlockMouseUp(object sender, MouseButtonEventArgs e)
        {
            var tag = ((TextBlock)sender).Text;
            tag = tag[0..(tag.IndexOf('|', 1) + 1)];
            var caretIndex = MaskTextBox.CaretIndex;
            MaskTextBox.Text = MaskTextBox.Text.Insert(caretIndex, tag);
            MaskTextBox.CaretIndex = caretIndex + tag.Length;
        }

        private async void HandleRenameButtonClick(object sender, RoutedEventArgs e)
        {
            if (_selectedPictures.Count > 1 && !RenameMask.Contains('|', StringComparison.Ordinal))
                throw new UserMessageException("Multiple files cannot be renamed to the same name.");
            RenameMask = RenameMask.Trim();
            _exampleNamer?.Dispose();
            _exampleNamer = null;
            int counter = 0;
            ProgressBarValue = 0;
            IsProgressBarVisible = true;
            IsEnabled = false;
            try
            {
                var selectedPictures = _selectedPictures.ToArray();
                int i = 0;
                foreach (var item in selectedPictures)
                {
                    string newName;
                    using (var namer = new MaskBasedNaming(item, counter++))
                        newName = namer.GetFileName(RenameMask);

                    // Allow overwriting when renaming single picture
                    if (_selectedPictures.Count == 1 && item.IsFile)
                    {
                        var overwritingFile = _allPictures.FirstOrDefault(f => f != item && f.IsFile && f.Name.Equals(newName, StringComparison.CurrentCultureIgnoreCase));
                        if (overwritingFile != null &&
                            MessageBox.Show($"The file {newName} already exists, do you want to overwrite it?", "Rename", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                        {
                            overwritingFile.Recycle(Settings.IncludeSidecarFiles && !IsExtensionWarningVisible);
                            _allPictures.Remove(overwritingFile);
                        }
                    }
                    await item.RenameAsync(newName, Settings.IncludeSidecarFiles && !IsExtensionWarningVisible);
                    _allPictures.Remove(item);
                    item.IsChecked = false;
                    _allPictures.InsertOrdered(item);
                    _selectedPictures.Remove(item);
                    ProgressBarValue = (double)(++i) / selectedPictures.Length;
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                IsEnabled = true;
                IsProgressBarVisible = false;
                if (counter > 0 && RenameMask.Contains('|', StringComparison.Ordinal))
                {
                    using var registrySettings = new RegistrySettings();
                    registrySettings.RenameMasks = string.Join('\\', 
                        (new[] { RenameMask }).Concat(_previousMasks).Distinct().Take(RenameHistoryLength));
                }
            }
            DialogResult = true;
        }

        public void Dispose()
        {
            _exampleNamer?.Dispose();
        }
    }
}
