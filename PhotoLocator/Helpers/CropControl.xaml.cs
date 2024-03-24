using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PhotoLocator.Helpers
{
    /// <summary> Interaction logic for CropControl.xaml </summary>
    public partial class CropControl : UserControl, INotifyPropertyChanged
    {
        private Point _previousMousePosition;

        public CropControl()
        {
            InitializeComponent();
            Reset();
        }

        public void Reset()
        {
            CropTopOffset = new(0.1, GridUnitType.Star);
            CropHeight = new(0.8, GridUnitType.Star);
            CropBottomOffset = new(0.1, GridUnitType.Star);
            CropLeftOffset = new(0.1, GridUnitType.Star);
            CropWidth = new(0.8, GridUnitType.Star);
            CropRightOffset = new(0.1, GridUnitType.Star);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RatioText)));
            return true;
        }

        public GridLength CropLeftOffset { get => _cropLeftOffset; set => SetProperty(ref _cropLeftOffset, value); }
        private GridLength _cropLeftOffset;

        public GridLength CropWidth { get => _cropWidth; set => SetProperty(ref _cropWidth, value); }
        private GridLength _cropWidth;

        public GridLength CropRightOffset { get => _cropRightOffset; set => SetProperty(ref _cropRightOffset, value); }
        private GridLength _cropRightOffset;

        public GridLength CropTopOffset { get => _cropTopOffset; set => SetProperty(ref _cropTopOffset, value); }
        private GridLength _cropTopOffset;

        public GridLength CropHeight { get => _cropHeight; set => SetProperty(ref _cropHeight, value); }
        private GridLength _cropHeight;
        public GridLength CropBottomOffset { get => _cropBottomOffset; set => SetProperty(ref _cropBottomOffset, value); }
        private GridLength _cropBottomOffset;

        public string? RatioText => $"{CropWidth.Value:F0}x{CropHeight.Value:F0} {(CropWidth.Value * ActualWidth) / (CropHeight.Value * ActualHeight):F2}";

        private void HandleCropMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                _previousMousePosition = e.GetPosition(this);
        }
        private void HandleCropMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var mousePosition = e.GetPosition(this);
                CropLeftOffset = new GridLength(CropLeftOffset.Value + mousePosition.X - _previousMousePosition.X);
                CropTopOffset = new GridLength(CropTopOffset.Value + mousePosition.Y - _previousMousePosition.Y);
                _previousMousePosition = mousePosition;
            }
        }
    }
}
