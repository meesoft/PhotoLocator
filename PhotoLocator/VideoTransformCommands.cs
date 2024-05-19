using Microsoft.VisualBasic;
using PhotoLocator.Helpers;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PhotoLocator
{
    public class VideoTransformCommands
    {
        private readonly IMainViewModel _mainViewModel;

        public VideoTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public ICommand ExtractFrames => new RelayCommand(async o =>
        {
            if (_mainViewModel.SelectedPicture is null)
                return;
            var inPath = _mainViewModel.SelectedPicture.FullPath;
            var outPath = Path.Combine(Path.GetDirectoryName(inPath)!, Path.GetFileNameWithoutExtension(inPath), "Frame%04d.jpg");
            outPath = Interaction.InputBox($"Output path:", "Extract frames", outPath);
            if (string.IsNullOrEmpty(outPath))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                await ProcessFileAsync($"-i \"{inPath}\" \"{outPath}\"");
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
            var outPath = Path.Combine(Path.GetDirectoryName(inPath)!, Path.GetFileNameWithoutExtension(inPath) + "[stab]" + Path.GetExtension(inPath));
            outPath = Interaction.InputBox($"Output path:", "Stabilize", outPath);
            if (string.IsNullOrEmpty(outPath))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(inPath)!);
                await ProcessFileAsync($"-i \"{inPath}\" -vf vidstabdetect=shakiness=7 -f null -");
                await ProcessFileAsync($"-i \"{inPath}\" {vfArgs} \"{outPath}\"");
                File.Delete("transforms.trf");
            }, "Processing");
        });


        private async Task ProcessFileAsync(string args, bool redirectStandardError = false)
        {
            var startInfo = new ProcessStartInfo(_mainViewModel.Settings.FFmpegPath ?? throw new UserMessageException("FFmpeg path must be set in Settings"), args);
            startInfo.RedirectStandardError = redirectStandardError;
            var process = Process.Start(startInfo) ?? throw new IOException("Failed to start FFmpeg");
            var output = redirectStandardError ? process.StandardError.ReadToEnd() : string.Empty; // We must read before waiting
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                if (redirectStandardError) 
                    throw new UserMessageException(output);
                throw new UserMessageException("Unable to process video. Command line:\n" + args);
            }
        }
    }
}
