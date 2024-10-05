using PhotoLocator.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace PhotoLocator
{
    /// <summary>
    /// Interaction logic for LocalContrastView.xaml
    /// </summary>
    public partial class LocalContrastView : Window
    {
        LocalContrastViewModel _viewModel = null!;
        ImageZoomPreviewViewHelper _zoomPreviewViewHelper = null!;

        public LocalContrastView()
        {
            InitializeComponent();
            DataContextChanged += HandleDataContextChanged;
        }

        private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            _viewModel = (LocalContrastViewModel)DataContext;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
                _zoomPreviewViewHelper = new ImageZoomPreviewViewHelper(PreviewCanvas, ZoomedPreviewImage, _viewModel);
                UpdatePreviewZoom();
            }
        }

        private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_viewModel.PreviewZoom))
                UpdatePreviewZoom();
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomInCommand.Execute(null);
            }
            else if (e.Delta < 0)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    _viewModel.ZoomOutCommand.Execute(null);
            }
            e.Handled = true;
        }

        private void UpdatePreviewZoom()
        {
            ZoomToFitItem.IsChecked = _viewModel.PreviewZoom == 0;
            Zoom100Item.IsChecked = _viewModel.PreviewZoom == 1;
            Zoom200Item.IsChecked = _viewModel.PreviewZoom == 2;
            Zoom400Item.IsChecked = _viewModel.PreviewZoom == 4;
            if (_viewModel.PreviewZoom == 0)
            {
                FullPreviewImage.Visibility = Visibility.Visible;
                ZoomedPreviewImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                ZoomedPreviewImage.Visibility = Visibility.Visible;
                UpdateLayout();
                _zoomPreviewViewHelper.InitializePreviewRenderTransform(true);
                FullPreviewImage.Visibility = Visibility.Collapsed;
            }
        }

        private void HandleOriginalButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.PreviewPictureSource = _viewModel.SourceBitmap;
        }

        private async void HandleOriginalButtonMouseUp(object sender, MouseButtonEventArgs e)
        {
            await _viewModel.UpdatePreviewAsync();
        }

        private async void HandleOkButtonClick(object sender, RoutedEventArgs e)
        {
            if (DialogResult is not null)
                return;
            await _viewModel.FinishPreviewAsync();
            DialogResult ??= true;
        }
    }
}
