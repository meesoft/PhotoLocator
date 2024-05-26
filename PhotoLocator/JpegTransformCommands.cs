using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    public class JpegTransformCommands
    {
        private readonly IMainViewModel _mainViewModel;

        public JpegTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public ICommand RotateLeftCommand => new RelayCommand(async o => await RotateSelectedAsync(270));

        public ICommand RotateRightCommand => new RelayCommand(async o => await RotateSelectedAsync(90));

        public ICommand Rotate180Command => new RelayCommand(async o => await RotateSelectedAsync(180));

        private async Task RotateSelectedAsync(int angle)
        {
            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile && JpegTransformations.IsFileTypeSupported(item.Name)).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("Unsupported file format");
            await _mainViewModel.RunProcessWithProgressBarAsync(progressCallback => Task.Run(() =>
            {
                int i = 0;
                foreach (var item in allSelected)
                {
                    JpegTransformations.Rotate(item.FullPath, item.GetProcessedFileName(), angle); //TODO: Make async
                    item.Rotation = Rotation.Rotate0;
                    item.IsChecked = false;
                    progressCallback((double)(++i) / allSelected.Length);
                }
            }), "Rotating...");
        }
    }
}
