using Microsoft.VisualBasic;
using Microsoft.Win32;
using PhotoLocator.Helpers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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

        private void ProcessStdError(string line)
        {
            Debug.WriteLine(line);
            _mainViewModel.ProgressBarText = line;
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
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" \"{outPath}\"", ProcessStdError);
            }, "Processing");
        });

        public ICommand StabilizeVideo => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;

            var vfArgs = "-vf \"vidstabtransform=smoothing=10:zoom=0, setpts=1*PTS\"";
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
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(inPath)!);
                await _videoTransforms.RunFFmpegAsync(
                    $"-i \"{inPath}\" -vf \"crop=2000:2160:0:0, vidstabdetect=shakiness=7:result=transforms.trf:show=1\" dummy.mp4", ProcessStdError);
                await _videoTransforms.RunFFmpegAsync(
                    $"-i \"{inPath}\" {vfArgs} \"{outPath}\"", ProcessStdError);
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
            outPath = Interaction.InputBox("Output path:", "Stabilize and extract frames", outPath);
            if (string.IsNullOrEmpty(outPath))
                return;

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(inPath)!);
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" -vf vidstabdetect=shakiness=7 -f null -", ProcessStdError);
                await _videoTransforms.RunFFmpegAsync($"-i \"{inPath}\" {vfArgs} \"{outPath}\"", ProcessStdError);
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
                File.Delete(outPath);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
                await File.WriteAllLinesAsync(inputFileName, allSelected.Select(f => $"file '{f.Name}'"));
                await _videoTransforms.RunFFmpegAsync($"-f concat -safe 0 -i {inputFileName} {args} \"{outPath}\"", ProcessStdError);
                File.Delete(inputFileName);
            }, "Processing");
        });

        public ICommand GenerateMaxFrame => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;
            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var args = $" -i \"{inPath}\" -c:v bmp -f image2pipe -";

            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(inPath);
            dlg.FileName = Path.Combine(dlg.InitialDirectory!, Path.GetFileNameWithoutExtension(inPath) + "[max].png");
            dlg.DefaultExt = ".png";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outPath = dlg.FileName;

            var maxFrames = Interaction.InputBox("Max number of frames:", "Generate Max frame", int.MaxValue.ToString(CultureInfo.CurrentCulture));
            if (string.IsNullOrEmpty(maxFrames))
                return;
            var process = new CombineFramesOperation(int.Parse(maxFrames, CultureInfo.CurrentCulture), default);

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateMax, ProcessStdError);
                SaveImage(process.GetAverageResult(), outPath);
            }, "Processing");
        });

        public ICommand GenerateAverageFrame => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;
            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var args = $" -i \"{inPath}\" -c:v bmp -f image2pipe -";

            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(inPath);
            dlg.FileName = Path.Combine(dlg.InitialDirectory!, Path.GetFileNameWithoutExtension(inPath) + "[avg].png");
            dlg.DefaultExt = ".png";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outPath = dlg.FileName;

            var maxFrames = Interaction.InputBox("Max number of frames:", "Generate Average frame", int.MaxValue.ToString(CultureInfo.CurrentCulture));
            if (string.IsNullOrEmpty(maxFrames))
                return;
            var process = new CombineFramesOperation(int.Parse(maxFrames, CultureInfo.CurrentCulture), default);

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateSum, ProcessStdError);
                SaveImage(process.GetAverageResult(), outPath);
            }, "Processing");
        });

        private static void SaveImage(BitmapSource result, string outPath)
        {
            var ext = Path.GetExtension(outPath).ToUpperInvariant();
            using var fileStream = new FileStream(outPath, FileMode.Create);
            BitmapEncoder encoder = ext == ".JPG" ? new JpegBitmapEncoder() : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(result));
            encoder.Save(fileStream);
        }
    }
}
