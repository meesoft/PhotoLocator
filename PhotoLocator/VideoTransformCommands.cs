using Microsoft.VisualBasic;
using Microsoft.Win32;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using PhotoLocator.Metadata;
using PhotoLocator.PictureFileFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        const string InputFileName = "input.txt";
        const string TransformsFileName = "transforms.trf";
        const string SaveVideoFilter = "MP4|*.mp4";
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
            _selectedVideoFormat = VideoFormats[DefaultVideoFormatIndex];
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

        public bool HasOnlyImageInput
        {
            get => _hasOnlyImageInput;
            set
            {
                if (SetProperty(ref _hasOnlyImageInput, value))
                    UpdateProcessArgs();
            }
        }
        bool _hasOnlyImageInput;

        public string SkipTo
        {
            get => _skipTo;
            set
            {
                if (SetProperty(ref _skipTo, value.Trim()))
                {
                    UpdateInputArgs();
                    UpdateOutputArgs();
                }
            }
        }
        string _skipTo = string.Empty;

        public string Duration
        {
            get => _duration;
            set
            {
                if (SetProperty(ref _duration, value.Trim()))
                {
                    UpdateInputArgs();
                    UpdateOutputArgs();
                }
            }
        }
        string _duration = string.Empty;

        public bool IsRotateChecked
        {
            get => _isRotateChecked;
            set
            {
                if (SetProperty(ref _isRotateChecked, value))
                    UpdateProcessArgs();
            }
        }
        bool _isRotateChecked;

        public string RotationAngle
        {
            get => _rotationAngle;
            set
            {
                if (SetProperty(ref _rotationAngle, value.Trim()))
                    UpdateProcessArgs();
            }
        }
        string _rotationAngle = string.Empty;

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
                if (SetProperty(ref _cropWindow, value.Replace(" ", "", StringComparison.Ordinal)))
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
                if (SetProperty(ref _scaleTo, value.Trim()))
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

        public bool IsBicubicStabilizeChecked
        {
            get => _isBicubicStabilizeChecked;
            set
            {
                if (SetProperty(ref _isBicubicStabilizeChecked, value))
                    UpdateProcessArgs();
            }
        }
        bool _isBicubicStabilizeChecked = true;

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
            set => SetProperty(ref _stabilizeArguments, value.Trim());
        }
        string _stabilizeArguments = string.Empty;

        public bool IsLocalContrastEnabled => OutputMode is OutputMode.Video or OutputMode.ImageSequence;

        public bool IsLocalContrastChecked
        {
            get => _localContrastSetup is not null;
            set
            {
                if (value)
                    SetupLocalContrastCommand.Execute(null);
                else
                    _localContrastSetup = null;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocalContrastChecked)));
                UpdateOutputArgs();
            }
        }

        public ICommand SetupLocalContrastCommand => new RelayCommand(o =>
        {
            _localContrastSetup ??= new LocalContrastViewModel();
            if (_localContrastSetup.SourceBitmap is null)
                using (var cursor = new MouseCursorOverride())
                {
                    var preview = _mainViewModel.GetSelectedItems().First(item => item.IsFile).LoadPreview(default) ?? throw new UserMessageException("Unable to load preview frame");
                    _localContrastSetup.SourceBitmap = preview;
                }
            var window = new LocalContrastView();
            window.CancelButton.Visibility = Visibility.Hidden;
            window.Owner = Application.Current.MainWindow.OwnedWindows.OfType<VideoTransformWindow>().FirstOrDefault(w => w.IsVisible);
            window.DataContext = _localContrastSetup;
            window.ShowDialog();
            window.DataContext = null;
            _localContrastSetup.SaveLastUsedValues();
        });
        LocalContrastViewModel? _localContrastSetup;

        public bool IsRollingAverageChecked
        {
            get => _isRollingAverageChecked;
            set
            {
                if (SetProperty(ref _isRollingAverageChecked, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCombineFramesOperation)));
                    UpdateOutputArgs();
                }
            }
        }
        bool _isRollingAverageChecked;

        public int RollingAverageFrames
        {
            get => _rollingAverageFrames;
            set => SetProperty(ref _rollingAverageFrames, Math.Max(1, value));
        }
        int _rollingAverageFrames = 10;

        public OutputMode OutputMode
        {
            get => _outputMode;
            set
            {
                if (SetProperty(ref _outputMode, value))
                {
                    UpdateProcessArgs();
                    UpdateOutputArgs();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCombineFramesOperation)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocalContrastEnabled)));
                }
            }
        }
        OutputMode _outputMode;

        public ComboBoxItem SelectedVideoFormat
        {
            get => _selectedVideoFormat;
            set
            {
                if (value is null)
                    return;
                if (SetProperty(ref _selectedVideoFormat, value))
                {
                    UpdateProcessArgs();
                    UpdateOutputArgs();
                }
            }
        }
        ComboBoxItem _selectedVideoFormat;

        public ObservableCollection<ComboBoxItem> VideoFormats { get; } = [
            new ComboBoxItem { Content = "Default" },
            new ComboBoxItem { Content = "Copy", Tag = "-c copy" },
            new ComboBoxItem { Content = "libx264", Tag = "-c:v libx264 -pix_fmt yuv420p" },
            new ComboBoxItem { Content = "libx265", Tag = "-c:v libx265 -pix_fmt yuv420p" },
            new ComboBoxItem { Content = "libvpx-vp9", Tag = "-c:v libvpx-vp9 -pix_fmt yuv420p" },
            new ComboBoxItem { Content = "libsvtav1", Tag = "-c:v libsvtav1 -pix_fmt yuv420p" },
        ];

        const int DefaultVideoFormatIndex = 3;
        const int CopyVideoFormatIndex = 1;

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value.Trim()))
                {
                    UpdateInputArgs();
                    UpdateOutputArgs();
                }
            }
        }
        string _frameRate = string.Empty;

        public string VideoBitRate
        {
            get => _videoBitRate;
            set
            {
                if (SetProperty(ref _videoBitRate, value.Trim()))
                    UpdateOutputArgs();
            }
        }
        string _videoBitRate = string.Empty;

        public bool IsRemoveAudioChecked
        {
            get => _isRemoveAudioChecked;
            set
            {
                if (SetProperty(ref _isRemoveAudioChecked, value))
                    UpdateOutputArgs();
            }
        }
        bool _isRemoveAudioChecked;

        public bool IsCombineFramesOperation => IsRollingAverageChecked || OutputMode is OutputMode.Average or OutputMode.Max;

        public bool IsRegisterFramesChecked
        {
            get => _isRegisterFramesChecked;
            set => SetProperty(ref _isRegisterFramesChecked, value);
        }
        bool _isRegisterFramesChecked;

        public string RegistrationRegion
        {
            get => _registrationRegion;
            set => SetProperty(ref _registrationRegion, value.Replace(" ", "", StringComparison.Ordinal));
        }
        string _registrationRegion = "w:h:x:y";

        ROI? ParseRegistrationRegion()
        {
            if (string.IsNullOrEmpty(RegistrationRegion) || RegistrationRegion[0] == 'w')
                return null;
            var parts = RegistrationRegion.Split(':');
            return new ROI
            {
                Left = int.Parse(parts[2], CultureInfo.CurrentCulture),
                Top = int.Parse(parts[3], CultureInfo.CurrentCulture),
                Width = int.Parse(parts[0], CultureInfo.CurrentCulture),
                Height = int.Parse(parts[1], CultureInfo.CurrentCulture),
            };
        }

        public string DarkFramePath
        {
            get => _darkFramePath;
            set => SetProperty(ref _darkFramePath, value.TrimPath());
        }
        string _darkFramePath = string.Empty;

        public string InputArguments
        {
            get => _inputArguments;
            set => SetProperty(ref _inputArguments, value.Trim());
        }
        string _inputArguments = string.Empty;

        public string ProcessArguments
        {
            get => _processArguments;
            set => SetProperty(ref _processArguments, value.Trim());
        }
        string _processArguments = string.Empty;

        public string OutputArguments
        {
            get => _outputArguments;
            set => SetProperty(ref _outputArguments, value.Trim());
        }
        string _outputArguments = string.Empty;

        private PictureItemViewModel[] UpdateInputArgs()
        {
            var allSelected = _mainViewModel.GetSelectedItems().Where(item => item.IsFile).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("No items selected");
            if (allSelected.Length == 1)
            {
                HasSingleInput = true;
                var args = string.Empty;
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
            HasOnlyImageInput = !allSelected.Any(item => item.IsVideo);
            return allSelected;
        }

        private void UpdateStabilizeArgs()
        {
            if (IsStabilizeChecked)
            {
                var filters = new List<string>();
                if (IsCropChecked)
                    filters.Add($"crop={CropWindow}");
                if (IsScaleChecked)
                    filters.Add($"scale={ScaleTo}");
                filters.Add($"vidstabdetect=shakiness=7{(IsTripodChecked ? ":tripod=1" : "")}:result={VideoTransformCommands.TransformsFileName}");
                StabilizeArguments = $"-vf \"{string.Join(", ", filters)}\" -f null -";
            }
            else
            {
                StabilizeArguments = string.Empty;
            }
        }

        private void UpdateProcessArgs()
        {
            var filters = new List<string>();
            if (IsRotateChecked)
                filters.Add($"rotate={RotationAngle}*PI/180");
            if (IsCropChecked)
                filters.Add($"crop={CropWindow}");
            if (IsScaleChecked)
                filters.Add($"scale={ScaleTo}");
            if (IsStabilizeChecked)
                filters.Add($"vidstabtransform=smoothing={SmoothFrames}"
                    + (IsTripodChecked ? ":tripod=1" : null)
                    + (IsBicubicStabilizeChecked ? ":interpol=bicubic" : null));
            if (HasOnlyImageInput)
                filters.Add("colorspace=all=bt709:iall=bt601-6-625:fast=1");
            if (filters.Count == 0)
                ProcessArguments = string.Empty;
            else
                ProcessArguments = $"-vf \"{string.Join(", ", filters)}\"";
        }

        private void UpdateOutputArgs()
        {
            if (OutputMode == OutputMode.Video)
            {
                var args = new List<string>();
                if (IsRemoveAudioChecked)
                    args.Add("-an");
                else if ((IsLocalContrastChecked || IsRollingAverageChecked) && HasSingleInput && !HasOnlyImageInput)
                {
                    if (!string.IsNullOrEmpty(SkipTo))
                        args.Add($"-ss {SkipTo}");
                    if (!string.IsNullOrEmpty(Duration))
                        args.Add($"-t {Duration}");
                    args.Add($"-i \"{_mainViewModel.GetSelectedItems().First(item => item.IsFile).FullPath}\" -map 0:v -map 1:a? -c:a copy");
                }
                if (SelectedVideoFormat.Tag is not null)
                    args.Add((string)SelectedVideoFormat.Tag);
                if (!string.IsNullOrEmpty(FrameRate))
                    args.Add($"-r {FrameRate}");
                if (!string.IsNullOrEmpty(VideoBitRate))
                    args.Add($"-b:v {VideoBitRate}M");
                else if ((SelectedVideoFormat.Tag as string)?.StartsWith("-c:v", StringComparison.Ordinal) == true)
                    args.Add("-crf 20");
                OutputArguments = string.Join(" ", args);
            }
            else
                OutputArguments = string.Empty;
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
                SelectedVideoFormat = VideoFormats[CopyVideoFormatIndex];
                FrameRate = "";
            }
            else
            {
                if (SelectedVideoFormat == VideoFormats[CopyVideoFormatIndex])
                    SelectedVideoFormat = VideoFormats[DefaultVideoFormatIndex];
                if (string.IsNullOrEmpty(FrameRate))
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
            dlg.Filter = SaveVideoFilter;
            dlg.DefaultExt = ".mp4";
            dlg.CheckPathExists = false;
            if (dlg.ShowDialog() != true)
                return;
            var outFileName = dlg.FileName;

            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                PrepareProgressDisplay(progressCallback);
                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} -filter_complex \"[0:v:0]pad=iw*2:ih[bg]; [bg][1:v:0]overlay=w\" -y \"{outFileName}\"";
                await _videoTransforms.RunFFmpegAsync(args, ProcessStdError, ct);
            }, "Processing");
            await _mainViewModel.SelectFileAsync(outFileName);
        });

        public ICommand GenerateMaxFrame => new RelayCommand(o =>
        {
            OutputMode = OutputMode.Max;
            SelectedVideoFormat = VideoFormats.First();
            ProcessSelected.Execute(null);
        });

        public ICommand GenerateAverageFrame => new RelayCommand(o =>
        {
            OutputMode = OutputMode.Average;
            SelectedVideoFormat = VideoFormats.First();
            ProcessSelected.Execute(null);
        });

        public ICommand ProcessSelected => new RelayCommand(async o =>
        {
            var allSelected = UpdateInputArgs();

            if (_localContrastSetup is not null && _localContrastSetup.SourceBitmap is not null)
                _localContrastSetup.SourceBitmap = null;
            var window = new VideoTransformWindow() { Owner = App.Current.MainWindow, DataContext = this };
            try
            {
                if (window.ShowDialog() != true)
                    return;
            }
            finally
            {
                if (_localContrastSetup is not null && _localContrastSetup.SourceBitmap is not null)
                    _localContrastSetup.SourceBitmap = null;
                window.DataContext = null;
            }

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
                var dlg = new SaveFileDialog();
                switch (OutputMode)
                {
                    case OutputMode.Video:
                        postfix = allSelected.Length > 1 ? "combined" : IsStabilizeChecked ? "stabilized" : "processed";
                        dlg.Filter = SaveVideoFilter;
                        ext = ".mp4";
                        break;
                    case OutputMode.Average:
                        postfix = "avg";
                        dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
                        dlg.FilterIndex = 2;
                        ext = ".png";
                        break;
                    case OutputMode.Max:
                        postfix = "max";
                        dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
                        dlg.FilterIndex = 2;
                        ext = ".png";
                        break;
                    default:
                        throw new ArgumentException("Unknown output mode");
                }
                dlg.InitialDirectory = inPath;
                dlg.FileName = Path.GetFileNameWithoutExtension(allSelected[0].Name) + $"[{postfix}]{ext}";
                dlg.DefaultExt = ext;
                dlg.CheckPathExists = false;
                if (dlg.ShowDialog() != true)
                    return;
                outFileName = dlg.FileName;
            }

            string? message = null;
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                var sw = Stopwatch.StartNew();
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
                    await File.WriteAllLinesAsync(InputFileName, allSelected.Select(f => $"file '{f.Name}'"), ct).ConfigureAwait(false);

                if (IsStabilizeChecked)
                {
                    _progressScale = 0.5;
                    File.Delete(TransformsFileName);
                    await _videoTransforms.RunFFmpegAsync($"{InputArguments} {StabilizeArguments}", ProcessStdError, ct).ConfigureAwait(false);
                    _progressOffset = 0.5;
                    _progressCallback?.Invoke(0.5);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} {ProcessArguments} {OutputArguments}";
                if (OutputMode is OutputMode.Average)
                {
                    using var process = new AverageFramesOperation(DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct);
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.ProcessImage, ProcessStdError, ct).ConfigureAwait(false);
                    if (process.Supports16BitResult() && Path.GetExtension(outFileName).ToUpperInvariant() is ".PNG" or ".TIF" or ".TIFF" or ".JXR")
                        GeneralFileFormatHandler.SaveToFile(process.GetResult16(), outFileName, CreateImageMetadata());
                    else
                        GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata());
                    message = $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
                }
                else if (OutputMode is OutputMode.Max)
                {
                    using var process = new MaxFramesOperation(DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct);
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.ProcessImage, ProcessStdError, ct).ConfigureAwait(false);
                    GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata());
                    message = $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
                }
                else if (IsLocalContrastChecked || IsRollingAverageChecked)
                {
                    if (!string.IsNullOrEmpty(FrameRate))
                    {
                        _fps = double.Parse(FrameRate, CultureInfo.InvariantCulture);
                        _hasFps = true;
                    }
                    var runningAverage = IsRollingAverageChecked ? new RollingAverageOperation(RollingAverageFrames, DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct) : null;
                    using var frameEnumerator = new QueueEnumerable<BitmapSource>();
                    var readTask = _videoTransforms.RunFFmpegWithStreamOutputImagesAsync($"{InputArguments} {ProcessArguments}", 
                        source =>
                        {
                            if (runningAverage is not null)
                            {
                                runningAverage.ProcessImage(source);
                                if (_localContrastSetup is null || _localContrastSetup.IsNoOperation)
                                {
                                    frameEnumerator.AddItem(runningAverage.GetResult8());
                                    return;
                                }
                                source = runningAverage.GetResult16();
                            }
                            frameEnumerator.AddItem(_localContrastSetup!.ApplyOperations(source));
                        }, ProcessStdError, ct);
                    await Task.WhenAny(frameEnumerator.GotFirst, Task.Delay(TimeSpan.FromSeconds(10), ct)).ConfigureAwait(false);
                    if (!_hasFps)
                        throw new UserMessageException("Unable to determine frame rate, please specify manually");
                    var writeTask = _videoTransforms.RunFFmpegWithStreamInputImagesAsync(_fps, $"{OutputArguments} -y \"{outFileName}\"", frameEnumerator, 
                        stdError => Debug.WriteLine("Writer: " + stdError), ct);
                    await await Task.WhenAny(readTask, writeTask).ConfigureAwait(false); // Write task is not expected to finish here, only if it fails
                    frameEnumerator.Break();
                    await writeTask.ConfigureAwait(false);
                }
                else
                {
                    await _videoTransforms.RunFFmpegAsync(args + $" -y \"{outFileName}\"", ProcessStdError, ct).ConfigureAwait(false);
                }
                _progressCallback?.Invoke(1);
                if (allSelected.Length > 1)
                    File.Delete(InputFileName);
                if (IsStabilizeChecked)
                    File.Delete(TransformsFileName);
            }, "Processing");
            await _mainViewModel.SelectFileAsync(outFileName);
            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(App.Current.MainWindow, message);
        });

        private BitmapMetadata? CreateImageMetadata()
        {
            var firstSelected = _mainViewModel.GetSelectedItems().First(item => item.IsFile);
            return ExifHandler.EncodePngMetadata(
                _hasDuration && _inputDuration.TotalSeconds >= 0.99 ? new Rational(IntMath.Round(_inputDuration.TotalSeconds), 1) : null,
                firstSelected.GeoTag,
                firstSelected.TimeStamp ?? File.GetLastWriteTime(firstSelected.FullPath));
        }

        private void PrepareProgressDisplay(Action<double>? progressCallback)
        {
            _progressCallback = progressCallback;
            _progressOffset = 0;
            _progressScale = 1;
            _frameCount = 0;
            if (double.TryParse(Duration, out var durationS))
            {
                _inputDuration = TimeSpan.FromSeconds(durationS);
                _hasDuration = true;
            }                
            else
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
                else if (!_hasDuration && line.StartsWith(VideoTransforms.DurationOutputPrefix, StringComparison.Ordinal))
                {
                    var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                        _hasDuration = TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out _inputDuration);
                }
                else if (!_hasFps && line.StartsWith(VideoTransforms.EncodingOutputPrefix, StringComparison.Ordinal))
                {
                    var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 1; i < parts.Length; i++)
                        if (parts[i] == "fps")
                        {
                            _hasFps = double.TryParse(parts[i - 1], CultureInfo.InvariantCulture, out _fps);
                            if (_hasDuration && _hasFps)
                                _frameCount = (int)(_inputDuration.TotalSeconds * _fps + 0.9);
                            break;
                        }
                }
            }
        }
    }
}
