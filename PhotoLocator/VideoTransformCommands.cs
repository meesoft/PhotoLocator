using MapControl;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PhotoLocator.Helpers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    public class VideoTransformCommands
    {
        readonly IMainViewModel _mainViewModel;
        readonly VideoTransforms _videoTransforms;

        public VideoTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _videoTransforms = new VideoTransforms(mainViewModel.Settings);
        }

        public ICommand ExtractFrames => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;
            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var outPath = Path.Combine(Path.GetDirectoryName(inPath)!, Path.GetFileNameWithoutExtension(inPath), "%06d.jpg");
            outPath = Interaction.InputBox($"Output path:", "Extract frames", outPath);
            if (string.IsNullOrEmpty(outPath))
                return;
            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" \"{outPath}\"");
            }, "Processing");
        });

        public ICommand Stabilize => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;

            var vfArgs = "-vf \"vidstabtransform=smoothing=10:zoom=0:input=transforms.trf, setpts=1*PTS\"";
            vfArgs = Interaction.InputBox($"Video filter arguments:", "Stabilize", vfArgs);
            if (string.IsNullOrEmpty(vfArgs))
                return;

            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var dlg = new SaveFileDialog();
            dlg.Title = "Stabilize";
            dlg.InitialDirectory = Path.GetDirectoryName(inPath);
            dlg.FileName = Path.Combine(dlg.InitialDirectory!, Path.GetFileNameWithoutExtension(inPath) + "[stabilized]" + Path.GetExtension(inPath));
            dlg.DefaultExt = ".mp4";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outPath = dlg.FileName;

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(inPath)!);
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" -vf vidstabdetect=shakiness=7 -f null -");
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" {vfArgs} \"{outPath}\"");
                File.Delete("transforms.trf");
            }, "Processing");
        });

        public ICommand StabilizeAndExtract => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;

            var vfArgs = "-vf \"vidstabtransform=smoothing=0:zoom=0:input=transforms.trf\"";
            vfArgs = Interaction.InputBox($"Video filter arguments:", "Stabilize and extract frames", vfArgs);
            if (string.IsNullOrEmpty(vfArgs))
                return;

            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var outPath = Path.Combine(Path.GetDirectoryName(inPath)!, Path.GetFileNameWithoutExtension(inPath) + "[stab]", "%06d.jpg");
            outPath = Interaction.InputBox($"Output path:", "Stabilize and extract frames", outPath);
            if (string.IsNullOrEmpty(outPath))
                return;

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(inPath)!);
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" -vf vidstabdetect=shakiness=7 -f null -");
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" {vfArgs} \"{outPath}\"");
                File.Delete("transforms.trf");
            }, "Processing");
        });

        public ICommand Combine => new RelayCommand(async o =>
        {
            const string inputFileName = "input.txt";

            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("No images selected");

            var args = "-c:v libx264 -r 30 -pix_fmt yuv420p";
            args = Interaction.InputBox($"Video filter arguments:", $"Combine {allSelected.Length} images to video", args);
            if (string.IsNullOrEmpty(args))
                return;

            var outPath = Path.GetDirectoryName(allSelected[0].FullPath);
            var dlg = new SaveFileDialog();
            dlg.Title = "Combine";
            dlg.InitialDirectory = outPath;
            dlg.FileName = outPath + '\\' + Path.GetFileNameWithoutExtension(outPath) + ".mp4";
            dlg.DefaultExt = ".mp4";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            outPath = dlg.FileName;

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
                await File.WriteAllLinesAsync(inputFileName, allSelected.Select(f => $"file '{f.Name}'"));
                await _videoTransforms.RunFFmpegAsync($"-f concat -safe 0 -i {inputFileName} {args} \"{outPath}\"");
                File.Delete(inputFileName);
            }, "Processing");
        });

        public ICommand CalcMax => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;
            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var args = $" -i \"{inPath}\" -c:v bmp -f image2pipe -";

            var dlg = new SaveFileDialog();
            dlg.Title = "Max";
            dlg.InitialDirectory = Path.GetDirectoryName(inPath);
            dlg.FileName = Path.Combine(dlg.InitialDirectory!, Path.GetFileNameWithoutExtension(inPath) + "[max].png");
            dlg.DefaultExt = ".png";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outPath = dlg.FileName;

            var process = new CombineFramesOperation(10, default);

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(outPath)!);
                await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.ProcessImage);
                var result = process.GetResult();
                using (var fileStream = new FileStream(outPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(result));
                    encoder.Save(fileStream);
                }
            }, "Processing");

            MessageBox.Show($"{process.ProcessedImages} frames was processed");
        });
    }
}
