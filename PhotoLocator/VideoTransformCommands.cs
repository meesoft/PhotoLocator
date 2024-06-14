using Microsoft.VisualBasic;
using Microsoft.Win32;
using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
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
        Action<double>? _progressCallback;
        double _progressOffset, _progressScale;
        TimeSpan _inputDuration;
        double _fps;
        int _frameCount;
        bool _hasDuration, _hasFps;
        

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

        public bool HasSingleInput
        {
            get => _hasSingleInput;
            set => SetProperty(ref _hasSingleInput, value);
        }
        bool _hasSingleInput;

        public string SkipTo
        {
            get => _skipTo;
            set
            {
                if (SetProperty(ref _skipTo, value))
                    UpdateInputArgs();
            }
        }
        string _skipTo = "";

        public string Duration
        {
            get => _duration;
            set
            {
                if (SetProperty(ref _duration, value))
                    UpdateInputArgs();
            }
        }
        string _duration = "";

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

        public bool IsScaleChecked
        {
            get => _isScaleChecked;
            set
            {
                if (SetProperty(ref _isScaleChecked, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        bool _isScaleChecked;

        public string ScaleTo
        {
            get => _scaleTo;
            set
            {
                if (SetProperty(ref _scaleTo, value))
                {
                    UpdateStabilizeArgs();
                    UpdateProcessArgs();
                }
            }
        }
        string _scaleTo = "w:h";

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
            new ComboBoxItem { Content = "libx264", Tag = "-c:v libx264" },
            new ComboBoxItem { Content = "libx265", Tag = "-c:v libx265" },
            new ComboBoxItem { Content = "libvpx-vp9", Tag = "-c:v libvpx-vp9" },
            new ComboBoxItem { Content = "libaom-av1", Tag = "-c:v libaom-av1" },
        ];

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value))
                {
                    UpdateInputArgs();
                    UpdateOutputArgs();
                }
            }
        }
        string _frameRate = "";

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

        private PictureItemViewModel[] UpdateInputArgs()
        {
            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("No items selected");
            if (allSelected.Length == 1)
            {
                HasSingleInput = true;
                var args = "";
                if (!string.IsNullOrEmpty(SkipTo))
                    args += $"-ss {SkipTo} ";
                if (!string.IsNullOrEmpty(Duration))
                    args += $"-t {Duration} ";
                if (!string.IsNullOrEmpty(FrameRate))
                    args += $"-r {FrameRate} ";
                InputArguments = args + $"-i \"{allSelected[0].FullPath}\"";
            }
            else
            {
                HasSingleInput = false;
                InputArguments = (string.IsNullOrEmpty(FrameRate) ? "" : $"-r {FrameRate} ") + $"-f concat -safe 0 -i {VideoTransformCommands.InputFileName}";
            }
            return allSelected;
        }

        private void UpdateStabilizeArgs()
        {
            if (IsStabilizeChecked)
            {
                var filters = new List<string>();
                if (IsTrimChecked)
                    filters.Add($"trim={TrimRange}");
                if (IsCropChecked)
                    filters.Add($"crop={CropWindow}");
                if (IsScaleChecked)
                    filters.Add($"scale={ScaleTo}");
                filters.Add($"vidstabdetect=shakiness=7{(IsTripodChecked ? ":tripod=1" : "")}:result={VideoTransformCommands.TransformsFileName}");
                StabilizeArguments = $"-vf \"{string.Join(", ", filters)}\" -f null -";
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
            if (IsScaleChecked)
                filters.Add($"scale={ScaleTo}");
            if (IsStabilizeChecked)
                filters.Add($"vidstabtransform=smoothing={SmoothFrames}{(IsTripodChecked ? ":tripod=1" : "")}");
            //if (OutputMode == OutputMode.Video && SelectedVideoFormat.Content.ToString() != "Copy")
            //    filters.Add("setpts=1*PTS");
            if (filters.Count == 0)
                ProcessArguments = "";
            else
                ProcessArguments = $"-vf \"{string.Join(", ", filters)}\"";
        }

        private void UpdateOutputArgs()
        {
            if (OutputMode == OutputMode.Video)
                OutputArguments = (string)SelectedVideoFormat.Tag + (string.IsNullOrEmpty(FrameRate) ? "" : $" -r {FrameRate}");
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
            {
                SelectedVideoFormat = VideoFormats.First(f => f.Content.ToString() == "Copy");
                FrameRate = "";
            }
            else
            {
                SelectedVideoFormat = VideoFormats.First(f => f.Content.ToString() == "libx264");
                FrameRate = "30";
            }
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
                PrepareProgressDisplay(progressCallback);
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
            var allSelected = UpdateInputArgs();

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
                        postfix = allSelected.Length > 1 ? "combined" : IsStabilizeChecked ? "stabilized" : "processed";
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

            string? message = null;
            await _mainViewModel.RunProcessWithProgressBarAsync(async progressCallback =>
            {
                progressCallback(-1);
                if (allSelected.All(item => !item.IsVideo))
                {
                    PrepareProgressDisplay(progressCallback);
                    _frameCount = allSelected.Length;
                }
                else 
                    PrepareProgressDisplay(allSelected.Length == 1 ? progressCallback : null);

                Directory.SetCurrentDirectory(inPath);
                if (allSelected.Length > 1)
                    await File.WriteAllLinesAsync(InputFileName, allSelected.Select(f => $"file '{f.Name}'"));
                
                if (IsStabilizeChecked)
                {
                    _progressScale = 0.5;
                    File.Delete(TransformsFileName);
                    await _videoTransforms.RunFFmpegAsync($"{InputArguments} {StabilizeArguments}", ProcessStdError);
                    _progressOffset = 0.5;
                    _progressCallback?.Invoke(0.5);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} {ProcessArguments} {OutputArguments}";
                if (OutputMode is OutputMode.Average)
                {
                    var process = new CombineFramesOperation();
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateSum, ProcessStdError);
                    SaveImage(process.GetAverageResult(), outFileName);
                    message = $"Processed {process.ProcessedImages} frames";
                }
                else if (OutputMode is OutputMode.Max)
                {
                    var process = new CombineFramesOperation();
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.UpdateMax, ProcessStdError);
                    SaveImage(process.GetResult(), outFileName);
                    message = $"Processed {process.ProcessedImages} frames";
                }
                else
                {
                    await _videoTransforms.RunFFmpegAsync(args + $" -y \"{outFileName}\"", ProcessStdError);
                }
                _progressCallback?.Invoke(1);
                if (allSelected.Length > 1)
                    File.Delete(InputFileName);
                if (IsStabilizeChecked)
                    File.Delete(TransformsFileName);
            }, "Processing");
            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(message);
        });

        private void PrepareProgressDisplay(Action<double>? progressCallback)
        {
            _progressCallback = progressCallback;
            _progressOffset = 0;
            _progressScale = 1;
            _frameCount = 0;
            _hasDuration = false;
            _hasFps = false;
        }

        private void ProcessStdError(string line)
        {
            Debug.WriteLine(line);
            _mainViewModel.ProgressBarText = line;

            if (_progressCallback is not null)
            {
                if (_frameCount > 0)
                {
                    if (line.StartsWith("frame=", StringComparison.Ordinal))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out var frame))
                            _progressCallback(_progressOffset + frame * _progressScale / _frameCount);
                    }
                }
                //Duration: 00:00:10.44, start: 0.000000, bitrate: 67364 kb / s
                //Stream #0:0[0x1](eng): Video: h264 (High) (avc1 / 0x31637661), yuv420p(tv, bt709, progressive), 3840x2160 [SAR 1:1 DAR 16:9], 67360 kb/s, 25 fps, 25 tbr, 12800 tbn (default)
                else if (!_hasDuration && line.StartsWith("  Duration: ", StringComparison.Ordinal))
                {
                    var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                        _hasDuration = TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out _inputDuration);
                }
                else if (!_hasFps && line.StartsWith("  Stream #0:", StringComparison.Ordinal))
                {
                    var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 1; i < parts.Length; i++)
                        if (parts[i] == "fps")
                        {
                            _hasFps = double.TryParse(parts[i - 1], CultureInfo.InvariantCulture, out _fps);
                            if (_hasDuration && _hasFps )
                                _frameCount = (int)(_inputDuration.TotalSeconds * _fps);
                            break;
                        }
                }
            }
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
