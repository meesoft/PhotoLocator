using Microsoft.VisualBasic;
using Microsoft.Win32;
using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoLocator
{
    public enum OutputMode
    {
        Video,
        ImageSequence,
        Average,
        Max,
    }

    public class VideoTransformCommands : INotifyPropertyChanged
    {
        public const string InputFileName = "input.txt";
        public const string TransformsFileName = "transforms.trf";

        readonly IMainViewModel _mainViewModel;
        readonly VideoTransforms _videoTransforms;

        public VideoTransformCommands(IMainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _videoTransforms = new VideoTransforms(mainViewModel.Settings);

            UpdateProcessArgs();
            UpdateOutputArgs();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public bool IsStabilizeChecked
        {
            get => _isStabilizeChecked;
            set
            {
                if (SetProperty(ref _isStabilizeChecked, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        bool _isStabilizeChecked;

        public bool IsTripodChecked
        {
            get => _isTripodChecked;
            set
            {
                if (SetProperty(ref _isTripodChecked, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        bool _isTripodChecked;

        public int SmoothFrames
        {
            get => _smoothFrames;
            set
            {
                if (SetProperty(ref _smoothFrames, value))
                    UpdateProcessArgs();
            }
        }
        int _smoothFrames = 10;

        public string StabilizeArguments
        {
            get => _stabilizeArguments;
            set => SetProperty(ref _stabilizeArguments, value);
        }
        string _stabilizeArguments = "";

        public bool IsTrimChecked
        {
            get => _isTrimChecked;
            set
            {
                if (SetProperty(ref _isTrimChecked, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        bool _isTrimChecked;

        public string TrimRange
        {
            get => _trimRange;
            set
            {
                if (SetProperty(ref _trimRange, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        string _trimRange = "start:end";

        public bool IsCropChecked
        {
            get => _isCropChecked;
            set
            {
                if (SetProperty(ref _isCropChecked, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        bool _isCropChecked;

        public string CropWindow
        {
            get => _cropWindow;
            set
            {
                if (SetProperty(ref _cropWindow, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        string _cropWindow = "w:h:x:y";

        public OutputMode OutputMode
        {
            get => _outputMode;
            set
            {
                if (SetProperty(ref _outputMode, value))
                {
                    UpdateProcessArgs();
                    UpdateOutputArgs();
                }
            }
        }
        OutputMode _outputMode;

        public ComboBoxItem SelectedVideoFormat
        {
            get => _selectedVideoFormat;
            set
            {
                if (SetProperty(ref _selectedVideoFormat, value))
                {
                    UpdateProcessArgs();
                    UpdateOutputArgs();
                }
            }
        }
        ComboBoxItem _selectedVideoFormat = VideoFormats.First();

        public static IEnumerable<ComboBoxItem> VideoFormats { get; } = [
            new ComboBoxItem { Content = "Default", Tag = "" },
            new ComboBoxItem { Content = "Copy", Tag = "-c copy" },
            new ComboBoxItem { Content = "libx264", Tag = "-c:v libx264 -r 30" },
            new ComboBoxItem { Content = "libx265", Tag = "-c:v libx265" },
            new ComboBoxItem { Content = "libvpx-vp9", Tag = "-c:v libvpx-vp9" },
            new ComboBoxItem { Content = "libaom-av1", Tag = "-c:v libaom-av1" },
        ];

        public string InputArguments
        {
            get => _inputArguments;
            set => SetProperty(ref _inputArguments, value);
        }
        string _inputArguments = "";

        public string ProcessArguments
        {
            get => _processArguments;
            set => SetProperty(ref _processArguments, value);
        }
        string _processArguments = "";

        public string OutputArguments
        {
            get => _outputArguments;
            set => SetProperty(ref _outputArguments, value);
        }
        string _outputArguments = "";

        private void UpdateInputArgs(PictureItemViewModel[] allSelected)
        {
            if (allSelected.Length == 0)
                throw new UserMessageException("No items selected");
            if (allSelected.Length == 1)
                InputArguments = $"-i \"{allSelected[0].FullPath}\"";
            else
                InputArguments = $"-f concat -safe 0 -i {VideoTransformCommands.InputFileName}";
        }

        private void UpdateStabilizeArgs()
        {
            if (IsStabilizeChecked)
            {
                var vfArgs = "";
                if (IsTrimChecked)
                    vfArgs += $"trim={TrimRange}, ";
                if (IsCropChecked)
                    vfArgs += $"crop={CropWindow}, ";

                vfArgs += $"vidstabdetect=shakiness=7:result={VideoTransformCommands.TransformsFileName}";
                if (IsTripodChecked)
                    vfArgs += ":tripod=1";

                StabilizeArguments = $"-vf \"{vfArgs}\" -f null -";
            }
            else
            {
                StabilizeArguments = "";
            }
        }

        private void UpdateProcessArgs()
        {
            var filters = new List<string>();
            if (IsTrimChecked)
                filters.Add($"trim={TrimRange}");
            if (IsCropChecked)
                filters.Add($"crop={CropWindow}");
            if (IsStabilizeChecked)
            {
                var vfArg = $"vidstabtransform=smoothing={SmoothFrames}";
                if (IsTripodChecked)
                    vfArg += ":tripod=1";
                filters.Add(vfArg);
            }
            if (OutputMode == OutputMode.Video && SelectedVideoFormat.Content.ToString() != "Copy")
                filters.Add("setpts=1*PTS");

            if (filters.Count == 0)
                ProcessArguments = "";
            else
                ProcessArguments = $"-vf \"{string.Join(", ", filters)}\"";
        }

        private void UpdateOutputArgs()
        {
            if (OutputMode == OutputMode.Video)
                OutputArguments = (string)SelectedVideoFormat.Tag;
            else 
                OutputArguments = "";
        }

        public ICommand ExtractFrames => new RelayCommand(o =>
        {
            OutputMode = OutputMode.ImageSequence;
            ProcessSelected.Execute(null);
        });

        public ICommand StabilizeVideo => new RelayCommand(o =>
        {
            IsStabilizeChecked = true;
            ProcessSelected.Execute(null);
        });

        public ICommand Combine => new RelayCommand(o =>
        {
            if (_mainViewModel.GetSelectedItems().All(item => item.IsVideo))
                SelectedVideoFormat = VideoFormats.First(f => f.Content.ToString() == "Copy");
            else
                SelectedVideoFormat = VideoFormats.First(f => f.Content.ToString() == "libx264");
            ProcessSelected.Execute(null);
        });

        public ICommand Compare => new RelayCommand(async o =>
        {
            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile).ToArray();
            if (allSelected.Length != 2)
                throw new UserMessageException("Select exactly 2 files");
            InputArguments = $"-i \"{allSelected[0].FullPath}\" -i \"{allSelected[1].FullPath}\"";

            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(allSelected[0].FullPath);
            dlg.FileName = Path.GetFileNameWithoutExtension(allSelected[0].Name) + "[compare].mp4";
            dlg.DefaultExt = ".mp4";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outFileName = dlg.FileName;

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} -filter_complex \"[0:v:0]pad=iw*2:ih[bg]; [bg][1:v:0]overlay=w\" -y \"{outFileName}\"";
                await _videoTransforms.RunFFmpegAsync(args, ProcessStdError);
            }, "Processing");
        });

        public ICommand GenerateMaxFrame => new RelayCommand(o =>
        {
            OutputMode = OutputMode.Max;
            ProcessSelected.Execute(null);
        });

        public ICommand GenerateAverageFrame => new RelayCommand(o =>
        {
            OutputMode = OutputMode.Average;
            ProcessSelected.Execute(null);
        });

        public ICommand ProcessSelected => new RelayCommand(async o =>
        {
            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile).ToArray();
            UpdateInputArgs(allSelected);

            var window = new VideoTransformWindow() { Owner = App.Current.MainWindow, DataContext = this };
            if (window.ShowDialog() != true)
                return;

            var inPath = Path.GetDirectoryName(allSelected[0].FullPath)!;
            string outFileName;
            if (OutputMode == OutputMode.ImageSequence)
            {
                outFileName = Path.Combine(inPath, Path.GetFileNameWithoutExtension(allSelected[0].Name), "%06d.jpg");
                outFileName = Interaction.InputBox($"Output path:", "Extract frames", outFileName);
                if (string.IsNullOrEmpty(outFileName))
                    return;
            }
            else
            {
                string postfix, ext;
                switch (OutputMode)
                {
                    case OutputMode.Video:
                        postfix = IsStabilizeChecked ? "stabilized" : "processed";
                        ext = ".mp4";
                        break;
                    case OutputMode.Average:
                        postfix = "avg";
                        ext = ".png";
                        break;
                    case OutputMode.Max:
                        postfix = "max";
                        ext = ".png";
                        break;
                    default:
                        throw new ArgumentException("Unknown output mode");
                }
                var dlg = new SaveFileDialog();
                dlg.InitialDirectory = inPath;
                dlg.FileName = Path.GetFileNameWithoutExtension(allSelected[0].Name) + $"[{postfix}]{ext}";
                dlg.DefaultExt = ext;
                dlg.CheckPathExists = false;
                if (dlg.ShowDialog() != true)
                    return;
                outFileName = dlg.FileName;
            }

            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                Directory.SetCurrentDirectory(inPath);
                if (allSelected.Length > 1)
                    await File.WriteAllLinesAsync(InputFileName, allSelected.Select(f => $"file '{f.Name}'"));
                
                if (IsStabilizeChecked)
                {
                    File.Delete(TransformsFileName);
                    await _videoTransforms.RunFFmpegAsync($"{InputArguments} {StabilizeArguments}", ProcessStdError);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} {ProcessArguments} {OutputArguments}";
                if (OutputMode is OutputMode.Average)
                {
                    var process = new CombineFramesOperation();
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateSum, ProcessStdError);
                    SaveImage(process.GetAverageResult(), outFileName);
                }
                else if (OutputMode is OutputMode.Max)
                {
                    var process = new CombineFramesOperation();
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateMax, ProcessStdError);
                    SaveImage(process.GetResult(), outFileName);
                }
                else
                {
                    await _videoTransforms.RunFFmpegAsync(args + $" -y \"{outFileName}\"", ProcessStdError);
                }
                if (allSelected.Length > 1)
                    File.Delete(InputFileName);
                if (IsStabilizeChecked)
                    File.Delete(TransformsFileName);
            }, "Processing");
        });

        private void ProcessStdError(string line)
        {
            Debug.WriteLine(line);
            _mainViewModel.ProgressBarText = line;
        }

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
