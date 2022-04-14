using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
                        ExampleName = GetFileName(_selectedPictures[0], RenameMask);
                        ErrorMessage = null;
                    }
                    catch(Exception ex)
                    {
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
            var text = ((TextBlock)sender).Text;
            RenameMask += text[0..(text.IndexOf('|', 1) + 1)];
        }

        private void HandleRenameButtonClick(object sender, RoutedEventArgs e)
        {
            if (_selectedPictures.Count > 1 && !RenameMask.Contains('|'))
                throw new Exception("Multiple files cannot be renamed to the same");
            foreach (var item in _selectedPictures)
            {
                var newName = GetFileName(item, RenameMask);
                var newFullPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
                File.Move(item.FullPath, newFullPath);
                item.Name = newName;
                item.FullPath = newFullPath;
            }
            if (RenameMask.Contains('|'))
            {
                using var settings = new RegistrySettings();
                settings.RenameMasks = String.Join('\\', (new[] { RenameMask }).Concat(_previousMasks).Distinct().Take(10));
            }
            DialogResult = true;
        }

        private static string GetFileName(PictureItemViewModel file, string mask)
        {
            DateTime GetTimestamp()
            {
                if (file.TimeStamp.HasValue)
                    return file.TimeStamp.Value;
                return File.GetCreationTimeUtc(file.FullPath);
            }

            var result = new StringBuilder();
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] == '|')
                {
                    int iEnd = mask.IndexOf('|', i + 1);
                    if (iEnd < 0)
                        throw new ArgumentException($"Tag at {i} not closed");
                    var tag = mask[(i + 1)..iEnd];
                    if (tag == "ext")
                        result.Append(Path.GetExtension(file.Name));
                    else if (tag == "*")
                        result.Append(Path.GetFileNameWithoutExtension(file.Name));
                    else if (tag == "D")
                        result.Append(GetTimestamp().ToString("yyyy-MM-dd"));
                    else if (tag == "T")
                        result.Append(GetTimestamp().ToString("HH.mm.ss"));
                    else if (tag == "DT")
                        result.Append(GetTimestamp().ToString("yyyy-MM-dd HH.mm.ss"));
                    else if (tag.EndsWith('?'))
                    {
                        var iFirstWildcard = tag.IndexOf('?');
                        var nChars = tag.Length - iFirstWildcard;
                        var prefix = tag[0..iFirstWildcard];
                        var iPrefix = file.Name.IndexOf(prefix);
                        if (iPrefix < 0)
                            throw new ArgumentException($"Search string '{prefix}' not found in name '{file.Name}'");
                        result.Append(file.Name.AsSpan(iPrefix + prefix.Length, nChars));
                    }
                    else
                        throw new ArgumentException($"Unsupported tag |{tag}|");
                    i = iEnd;
                }
                else
                    result.Append(mask[i]);
            }
            return result.ToString();
        }
    }
}
