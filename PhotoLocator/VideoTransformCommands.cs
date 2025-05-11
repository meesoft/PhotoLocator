﻿using Microsoft.Win32;
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

    public enum RollingAverageMode
    {
        None,
        RollingAverage,
        FadingAverage,
        FadingMax,
    }

    public class VideoTransformCommands : INotifyPropertyChanged
    {
        const string InputListFileName = "input.txt";
        const string TransformsFileName = "transforms.trf";
        const string SaveVideoFilter = "MP4|*.mp4";
        readonly IMainViewModel _mainViewModel;
        readonly VideoProcessing _videoTransforms;
        Action<double>? _progressCallback;
        double _progressOffset, _progressScale;
        TimeSpan _inputDuration;
        double _fps;
        int _frameCount;
        bool _hasDuration, _hasFps;

        public VideoTransformCommands(IMainViewModel mainViewModel)
        {
            _selectedVideoFormat = VideoFormats[DefaultVideoFormatIndex];
            _selectedEffect = Effects[0];
            _mainViewModel = mainViewModel;
            _videoTransforms = new VideoProcessing(mainViewModel.Settings);
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
                    if (_localContrastSetup is not null)
                        _localContrastSetup.SourceBitmap = null;
                    _mainViewModel.UpdatePreviewPictureAsync(_skipTo).WithExceptionLogging();
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

        public bool IsSpeedupChecked
        {
            get => _isSpeedupChecked;
            set
            {
                if (SetProperty(ref _isSpeedupChecked, value))
                    UpdateProcessArgs();
            }
        }
        bool _isSpeedupChecked;

        public string SpeedupBy
        {
            get => _speedupBy;
            set
            {
                if (SetProperty(ref _speedupBy, value.Trim()))
                    UpdateProcessArgs();
            }
        }
        string _speedupBy = string.Empty;

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

        public ObservableCollection<ComboBoxItem> Effects { get; } = [
            new ComboBoxItem { Content = "None" },
            new ComboBoxItem { Content = "Rotate 90° clockwise", Tag = "transpose=1" },
            new ComboBoxItem { Content = "Rotate 90° counterclockwise", Tag = "transpose=2" },
            new ComboBoxItem { Content = "Rotate 180°", Tag = "transpose=2,transpose=2" },
            new ComboBoxItem { Content = "Mirror left half to right", Tag = "crop=iw/2:ih:0:0,split[left][tmp];[tmp]hflip[right];[left][right] hstack" },
            new ComboBoxItem { Content = "Mirror top half to bottom", Tag = "crop=iw:ih/2:0:0,split[top][tmp];[tmp]vflip[bottom];[top][bottom] vstack" },
            new ComboBoxItem { Content = "Zoom", Tag = ( "scale=4*iw:4*ih, zoompan=z='if(lte(it,0),1,min(pzoom+{0},10))':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1920x1080:fps=30", "0.001" ) },
            new ComboBoxItem { Content = "Normalize", Tag = ( "normalize=smoothing={0}:independence=0", "50" ) },
            new ComboBoxItem { Content = "Saturation", Tag = ( "eq=saturation={0}", "1.3" ) },
            new ComboBoxItem { Content = "Contrast", Tag = ( "eq=brightness=0.05:contrast={0}", "1.3" ) },
            new ComboBoxItem { Content = "Denoise (atadenoise)", Tag = ( "atadenoise=s={0}", "9" ) },
            new ComboBoxItem { Content = "Denoise (hqdn3d)", Tag = ( "hqdn3d=luma_spatial={0}", "4" ) },
            new ComboBoxItem { Content = "Denoise (nlmeans)", Tag = ( "nlmeans=s={0}", "1.0" ) },
            new ComboBoxItem { Content = "Noise", Tag = ( "noise=c0s={0}:c0f=t+u", "60" ) },
            new ComboBoxItem { Content = "Sharpen", Tag = ( "unsharp=7:7:{0}", "2.5" ) },
        ];

        public ComboBoxItem SelectedEffect
        {
            get => _selectedEffect;
            set
            {
                if (value is null)
                    return;
                if (SetProperty(ref _selectedEffect, value))
                {
                    if (_selectedEffect.Tag is ValueTuple<string, string> effectTuple)
                    {
                        IsParameterizedEffect = true;
                        _effectStrength = effectTuple.Item2;
                    }
                    else
                    {
                        IsParameterizedEffect = false;
                        _effectStrength = null;
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectStrength)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsParameterizedEffect)));
                    UpdateProcessArgs();
                }
            }
        }
        ComboBoxItem _selectedEffect;

        public bool IsParameterizedEffect { get; private set; }

        public string? EffectStrength
        {
            get => _effectStrength;
            set
            {
                if (SetProperty(ref _effectStrength, value?.Trim().Replace(',', '.')))
                    UpdateProcessArgs();
            }
        }
        string? _effectStrength;

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
        int _smoothFrames = 20;

        public string StabilizeArguments
        {
            get => _stabilizeArguments;
            set => SetProperty(ref _stabilizeArguments, value.Trim());
        }
        string _stabilizeArguments = string.Empty;

        public bool IsFrameProcessingEnabled => OutputMode == OutputMode.Video && SelectedVideoFormat != VideoFormats[CopyVideoFormatIndex]
            || OutputMode == OutputMode.ImageSequence;

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
                    var firstSelected = _mainViewModel.GetSelectedItems(true).First();
                    var preview = firstSelected.LoadPreview(default, skipTo: HasSingleInput && !string.IsNullOrEmpty(SkipTo) ? SkipTo: null) 
                        ?? throw new UserMessageException("Unable to load preview frame");
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

        public RollingAverageMode RollingAverageMode
        {
            get => _rollingAverageMode;
            set
            {
                if (SetProperty(ref _rollingAverageMode, value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCombineFramesOperation)));
                    UpdateOutputArgs();
                }
            }
        }
        RollingAverageMode _rollingAverageMode;

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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrameProcessingEnabled)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrameProcessingEnabled)));
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
                    UpdateProcessArgs();
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

        public bool IsCombineFramesOperation => RollingAverageMode > RollingAverageMode.None || OutputMode is OutputMode.Average or OutputMode.Max;

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
            var allSelected = _mainViewModel.GetSelectedItems(true).ToArray();
            if (allSelected.Length == 0)
                throw new UserMessageException("No files are selected");
            HasOnlyImageInput = !allSelected.Any(item => item.IsVideo);
            if (allSelected.Length == 1)
            {
                HasSingleInput = true;
                var args = string.Empty;
                if (!string.IsNullOrEmpty(SkipTo))
                    args += $"-ss {SkipTo} ";
                if (!string.IsNullOrEmpty(Duration))
                    args += $"-t {Duration} ";
                InputArguments = args + $"-i \"{allSelected[0].FullPath}\"";
            }
            else
            {
                HasSingleInput = false;
                var args = string.Empty;
                if (HasOnlyImageInput && !string.IsNullOrEmpty(FrameRate))
                    args += $"-r {FrameRate} ";
                InputArguments = args + $"-f concat -safe 0 -i {VideoTransformCommands.InputListFileName}";
            }
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
            if (SelectedEffect.Tag is string effect)
                filters.Add(effect);
            else if (_selectedEffect.Tag is ValueTuple<string, string> effectTuple)
                filters.Add(string.Format(CultureInfo.InvariantCulture, effectTuple.Item1, EffectStrength));
            if (IsSpeedupChecked)
                filters.Add($"setpts=PTS/({SpeedupBy})");
            if (!string.IsNullOrEmpty(FrameRate))
                filters.Add($"fps={FrameRate}");
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
                else if ((IsLocalContrastChecked || RollingAverageMode > RollingAverageMode.None) && HasSingleInput && !HasOnlyImageInput)
                {
                    if (!string.IsNullOrEmpty(SkipTo))
                        args.Add($"-ss {SkipTo}");
                    if (!string.IsNullOrEmpty(Duration))
                        args.Add($"-t {Duration}");
                    args.Add($"-i \"{_mainViewModel.GetSelectedItems(true).First().FullPath}\" -map 0:v -map 1:a? -c:a copy"); // Copy audio from first file
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
            if (_mainViewModel.GetSelectedItems(true).All(item => item.IsVideo))
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
            var allSelected = _mainViewModel.GetSelectedItems(true).ToArray();
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

            await using var pause = _mainViewModel.PauseFileSystemWatcher();
            await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
            {
                progressCallback(-1);
                PrepareProgressDisplay(progressCallback);
                Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
                var args = $"{InputArguments} -filter_complex \"[0:v:0]pad=iw*2:ih[bg]; [bg][1:v:0]overlay=w\" -y \"{outFileName}\"";
                await _videoTransforms.RunFFmpegAsync(args, ProcessStdError, ct);
            }, "Processing...");
            await _mainViewModel.AddOrUpdateItemAsync(outFileName, false, true);
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

        internal void CropSelected(Rect cropRectangle)
        {
            _cropWindow = $"{cropRectangle.Width:0}:{cropRectangle.Height:0}:{cropRectangle.X:0}:{cropRectangle.Y:0}";
            IsCropChecked = true;
            ProcessSelected.Execute(null);
        }

        public ICommand ProcessSelected => new RelayCommand(async o =>
        {
            var allSelected = UpdateInputArgs();
            if (HasSingleInput && !string.IsNullOrEmpty(SkipTo))
                _mainViewModel.UpdatePreviewPictureAsync(SkipTo).WithExceptionLogging();
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
                var outputPath = TextInputWindow.Show($"Output path:", text => !string.IsNullOrWhiteSpace(text), "Extract frames", 
                    Path.Combine(inPath, Path.GetFileNameWithoutExtension(allSelected[0].Name), "%06d.jpg"));
                if (string.IsNullOrEmpty(outputPath))
                    return;
                outFileName = outputPath;
            }
            else
            {
                var dlg = SetupSaveFileDialog(allSelected, inPath); 
                if (dlg.ShowDialog() != true)
                    return;
                outFileName = dlg.FileName;
            }

            await using var pause = _mainViewModel.PauseFileSystemWatcher();
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
                    await File.WriteAllLinesAsync(InputListFileName, allSelected.Select(f => $"file '{f.Name}'"), ct).ConfigureAwait(false);

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
                        GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata(), _mainViewModel.Settings.JpegQuality);
                    message = $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
                }
                else if (OutputMode is OutputMode.Max)
                {
                    using var process = new MaxFramesOperation(DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct);
                    await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync(args, process.ProcessImage, ProcessStdError, ct).ConfigureAwait(false);
                    GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata(), _mainViewModel.Settings.JpegQuality);
                    message = $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
                }
                else if (IsLocalContrastChecked || RollingAverageMode > RollingAverageMode.None)
                {
                    if (!string.IsNullOrEmpty(FrameRate))
                    {
                        _fps = double.Parse(FrameRate, CultureInfo.InvariantCulture);
                        _hasFps = true;
                    }
                    CombineFramesOperationBase? runningAverage = RollingAverageMode switch
                    {
                        RollingAverageMode.RollingAverage => new RollingAverageOperation(RollingAverageFrames, DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct),
                        RollingAverageMode.FadingAverage => new FadingAverageOperation(RollingAverageFrames, DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct),
                        RollingAverageMode.FadingMax => new FadingMaxOperation(RollingAverageFrames, DarkFramePath, IsRegisterFramesChecked, ParseRegistrationRegion(), ct),
                        _ => null,
                    };

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
                        stdError => Log.Write("Writer: " + stdError), ct);
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
                    File.Delete(InputListFileName);
                if (IsStabilizeChecked)
                    File.Delete(TransformsFileName);
            }, "Processing...");
            await _mainViewModel.AddOrUpdateItemAsync(outFileName, false, true);
            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(App.Current.MainWindow, message);
        });

        private SaveFileDialog SetupSaveFileDialog(PictureItemViewModel[] allSelected, string inPath)
        {
            string postfix, ext;
            var dlg = new SaveFileDialog();
            switch (OutputMode)
            {
                case OutputMode.Video:
                    if (RollingAverageMode == RollingAverageMode.RollingAverage && RollingAverageFrames > 1)
                        postfix = "rolling" + RollingAverageFrames;
                    else if (RollingAverageMode == RollingAverageMode.FadingAverage && RollingAverageFrames > 1)
                        postfix = "fadeavg" + RollingAverageFrames;
                    else if (RollingAverageMode == RollingAverageMode.FadingMax && RollingAverageFrames > 1)
                        postfix = "fademax" + RollingAverageFrames;
                    else if (IsStabilizeChecked || IsRegisterFramesChecked && RollingAverageMode > RollingAverageMode.None)
                        postfix = "stabilized";
                    else if (allSelected.Length > 1)
                        postfix = "combined";
                    else if (SelectedEffect.Tag is not null)
                        postfix = SelectedEffect.Content.ToString()!.Split(' ')[0].ToLowerInvariant();
                    else
                        postfix = "processed";
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
            return dlg;
        }

        private BitmapMetadata? CreateImageMetadata()
        {
            var firstSelected = _mainViewModel.GetSelectedItems(true).First();
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
            Log.Write(line);
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
                else if (!_hasDuration && line.StartsWith(VideoProcessing.DurationOutputPrefix, StringComparison.Ordinal))
                {
                    var parts = line.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                        _hasDuration = TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out _inputDuration);
                }
                else if (!_hasFps && line.StartsWith(VideoProcessing.EncodingOutputPrefix, StringComparison.Ordinal))
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
