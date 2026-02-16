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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoLocator;

public class VideoTransformCommands : INotifyPropertyChanged
{
    const string OpenImageFileFilter = "Image files|*.png;*.tif;*.bmp;*.jpg";
    const string InputListFileName = "input.txt";
    const string SaveVideoFilter = "MP4|*.mp4";
    internal const string TransformsFileName = "transforms.trf";
    const int DefaultAverageFramesCount = 20;
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
        _selectedTimeSliceDirection = TimeSliceDirections[0];
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
        get;
        set => SetProperty(ref field, value);
    }

    public bool HasOnlyImageInput
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    }

    public string SkipTo
    {
        get;
        set
        {
            if (!SetProperty(ref field, value.Trim()))
                return;
            UpdateInputArgs();
            UpdateOutputArgs();
            _localContrastSetup?.SourceBitmap = null;
            _mainViewModel.UpdatePreviewPictureAsync(SkipTo).WithExceptionLogging();
        }
    } = string.Empty;

    public string Duration
    {
        get;
        set
        {
            if (!SetProperty(ref field, value.Trim()))
                return;
            UpdateInputArgs();
            UpdateOutputArgs();
            if (string.IsNullOrEmpty(SkipTo))
                _mainViewModel.UpdatePreviewPictureAsync(Duration).WithExceptionLogging();
            else if (double.TryParse(SkipTo, CultureInfo.InvariantCulture, out var skipToSeconds) && double.TryParse(Duration, CultureInfo.InvariantCulture, out var durationSeconds))
                _mainViewModel.UpdatePreviewPictureAsync((skipToSeconds + durationSeconds).ToString(CultureInfo.InvariantCulture)).WithExceptionLogging();
        }
    } = string.Empty;

    public bool IsRotateChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    }

    public string RotationAngle
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
                UpdateProcessArgs();
        }
    } = string.Empty;

    public bool IsSpeedupChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    }

    public string SpeedupBy
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
                UpdateProcessArgs();
        }
    } = string.Empty;

    public bool IsCropChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateStabilizeArgs();
                UpdateProcessArgs();
            }
        }
    }

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
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    }

    public string ScaleTo
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
                UpdateProcessArgs();
        }
    } = "w:h";

    public static readonly string ZoomEffect = "Zoom";
    public static readonly string Crossfade = "Crossfade";

    public static ObservableCollection<TextComboBoxItem> Effects { get; } = [ // Note that default save file name uses first word of effect name
        new TextComboBoxItem { Text = "None" },
        new TextComboBoxItem { Text = "Rotate 90° clockwise", Tag = "transpose=1" },
        new TextComboBoxItem { Text = "Rotate 90° counterclockwise", Tag = "transpose=2" },
        new TextComboBoxItem { Text = "Rotate 180°", Tag = "transpose=2,transpose=2" },
        new TextComboBoxItem { Text = "Mirror left half to right", Tag = "crop=iw/2:ih:0:0,split[left][tmp];[tmp]hflip[right];[left][right] hstack" },
        new TextComboBoxItem { Text = "Mirror top half to bottom", Tag = "crop=iw:ih/2:0:0,split[top][tmp];[tmp]vflip[bottom];[top][bottom] vstack" },
        new TextComboBoxItem { Text = ZoomEffect, Tag = new ParameterizedFilter( "scale=4*iw:4*ih, zoompan=z='if(lte(it,0),1,min(pzoom+{0},10))':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s={1}:fps={2}", "Zoom speed", "0.001" ) },
        new TextComboBoxItem { Text = "Normalize", Tag = new ParameterizedFilter( "normalize=smoothing={0}:independence=0", "Smooth frames", "50" ) },
        new TextComboBoxItem { Text = "Midtones", Tag = new ParameterizedFilter( "colorbalance=rm={0}:gm={0}:bm={0}", "Strength", "0.05" ) },
        new TextComboBoxItem { Text = "Saturation", Tag = new ParameterizedFilter( "eq=saturation={0}", "Strength","1.3" ) },
        new TextComboBoxItem { Text = "Contrast and brightness", Tag = new ParameterizedFilter( "eq=brightness=0.05:contrast={0}", "Contrast", "1.3" ) },
        new TextComboBoxItem { Text = "Denoise (atadenoise)", Tag = new ParameterizedFilter( "atadenoise=s={0}", "Strength", "9" ) },
        new TextComboBoxItem { Text = "Denoise (hqdn3d)", Tag = new ParameterizedFilter( "hqdn3d=luma_spatial={0}", "Strength", "4" ) },
        new TextComboBoxItem { Text = "Denoise (nlmeans)", Tag = new ParameterizedFilter( "nlmeans=s={0}", "Strength", "1.0" ) },
        new TextComboBoxItem { Text = "Noise", Tag = new ParameterizedFilter( "noise=c0s={0}:c0f=t+u", "Strength", "60" ) },
        new TextComboBoxItem { Text = "Sharpen", Tag = new ParameterizedFilter( "unsharp=7:7:{0}", "Strength", "2.5" ) },
        new TextComboBoxItem { Text = "Reverse", Tag = "reverse" },
        //ffmpeg -i IMG_%3d.jpg -vf zoompan=d=(A+B)/B:s=WxH:fps=1/B,framerate=25:interp_start=0:interp_end=255:scene=100 -c:v mpeg4 -maxrate 5M -q:v 2 out.mp4
        new TextComboBoxItem { Text = Crossfade, Tag = new ParameterizedFilter("zoompan=d=(0.1+{0})/{0}:s={1}:fps=1/{0},framerate={2}:interp_start=0:interp_end=255:scene=100" , "Fade duration", "0.5") },
    ];

    public TextComboBoxItem SelectedEffect
    {
        get => _selectedEffect;
        set
        {
            if (value is null)
                return;
            if (SetProperty(ref _selectedEffect, value))
            {
                if (value.Tag is ParameterizedFilter effectFilter)
                {
                    IsParameterizedEffect = true;
                    EffectParameterText = effectFilter.ParameterText + ':';
                    _effectParameter = effectFilter.DefaultValue;
                }
                else
                {
                    IsParameterizedEffect = false;
                    EffectParameterText = null;
                    _effectParameter = null;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectParameter)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsParameterizedEffect)));
                UpdateProcessArgs();
            }
        }
    }
    TextComboBoxItem _selectedEffect;

    public bool IsParameterizedEffect { get; private set; }

    public string? EffectParameter
    {
        get => _effectParameter;
        set
        {
            if (SetProperty(ref _effectParameter, value?.Trim().Replace(',', '.')))
                UpdateProcessArgs();
        }
    }
    string? _effectParameter;

    public string? EffectParameterText
    {
        get; set => SetProperty(ref field, value);
    }

    public bool IsStabilizeChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateStabilizeArgs();
                UpdateProcessArgs();
            }
        }
    }

    public bool IsTripodChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateStabilizeArgs();
                UpdateProcessArgs();
            }
        }
    }

    public bool IsBicubicStabilizeChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    } = true;

    public int SmoothFrames
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateProcessArgs();
        }
    } = 20;

    public string StabilizeArguments
    {
        get;
        set => SetProperty(ref field, value.Trim());
    } = string.Empty;

    [MemberNotNullWhen(true, nameof(_localContrastSetup))]
    public bool IsLocalContrastChecked
    {
        get => field && _localContrastSetup is not null;
        set
        {
            field = value;
            if (value)
                SetupLocalContrastCommand.Execute(null);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocalContrastChecked)));
            UpdateOutputArgs();
        }
    }

    public ICommand SetupLocalContrastCommand => new RelayCommand(async o =>
    {
        _localContrastSetup ??= new LocalContrastViewModel();
        if (_localContrastSetup.SourceBitmap is null)
            using (var cursor = new MouseCursorOverride())
            {
                try
                {
                    var firstSelected = _mainViewModel.GetSelectedItems(true).First();
                    var preview = await firstSelected.LoadPreviewAsync(default, preservePixelFormat: true, skipTo: HasSingleInput && !string.IsNullOrEmpty(SkipTo) ? SkipTo : null)
                        ?? throw new FileFormatException("LoadPreview returned null");
                    _localContrastSetup.SourceBitmap = preview;
                }
                catch (Exception ex)
                {
                    ExceptionHandler.LogException(ex);
                    ExceptionHandler.ShowException(new UserMessageException("Unable to load preview frame"));
                    return;
                }
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

    public CombineFramesMode CombineFramesMode
    {
        get;
        set
        {
            var wasTimeSlice = field is CombineFramesMode.TimeSlice or CombineFramesMode.TimeSliceInterpolated;
            if (SetProperty(ref field, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCombineFramesOperation)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRegistrationEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberOfFramesHint)));
                var isTimeSlice = field is CombineFramesMode.TimeSlice or CombineFramesMode.TimeSliceInterpolated;
                if (wasTimeSlice != isTimeSlice)
                    CombineFramesCount = isTimeSlice ? 1 : DefaultAverageFramesCount;
                UpdateProcessArgs();
                UpdateOutputArgs();
            }
        }
    }

    public int CombineFramesCount
    {
        get;
        set
        {
            if (SetProperty(ref field, Math.Max(1, value)))
                UpdateProcessArgs();
        }
    } = DefaultAverageFramesCount;

    public string NumberOfFramesHint => CombineFramesMode < CombineFramesMode.TimeSlice ? "Number of frames to combine" : "Time slice video loops";

    public static ObservableCollection<ImageComboBoxItem> TimeSliceDirections
    {
        get
        {
            if (field is null)
            {
                var maps = new SelectionMapFunction[] {
                    TimeSliceSelectionMaps.LeftToRight,
                    TimeSliceSelectionMaps.RightToLeft,
                    TimeSliceSelectionMaps.TopToBottom,
                    TimeSliceSelectionMaps.BottomToTop,
                    TimeSliceSelectionMaps.TopLeftToBottomRight,
                    TimeSliceSelectionMaps.TopRightToBottomLeft,
                    TimeSliceSelectionMaps.Ellipse,
                    TimeSliceSelectionMaps.Clock,
                };
                field = [];
                foreach (var mapFunction in maps)
                {
                    var map = TimeSliceSelectionMaps.GenerateSelectionMap(30, 20, mapFunction);
                    //map.ProcessElementWise(p => (float)Math.Round(p, 1));
                    field.Add(new ImageComboBoxItem
                    {
                        Image = map.ToBitmapSource(96, 96, 1),
                        Tag = mapFunction
                    });
                }
                field.Add(new ImageComboBoxItem { Text = "Load from file" });
            }
            return field;
        }
    }

    public ImageComboBoxItem SelectedTimeSliceDirection
    {
        get => _selectedTimeSliceDirection;
        set
        {
            if (value is null)
                return;
            if (value.Tag is null or FloatBitmap)
            {
                var dialog = new OpenFileDialog { Filter = OpenImageFileFilter };
                if (dialog.ShowDialog() is not true)
                    return;
                using var cursor = new MouseCursorOverride();
                using var sourceStream = dialog.OpenFile();
                var image = GeneralFileFormatHandler.LoadFromStream(sourceStream, Rotation.Rotate0, int.MaxValue, true, default);
                var selectionMap = ConvertToGrayscaleOperation.ConvertToGrayscale(new FloatBitmap(image, 1), true);
                value.Tag = selectionMap;
            }
            SetProperty(ref _selectedTimeSliceDirection, value);
        }
    }
    ImageComboBoxItem _selectedTimeSliceDirection;

    public OutputMode OutputMode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is OutputMode.TimeSliceImage && !(CombineFramesMode is CombineFramesMode.TimeSlice or CombineFramesMode.TimeSliceInterpolated))
                    CombineFramesMode = CombineFramesMode.TimeSlice;

                UpdateProcessArgs();
                UpdateOutputArgs();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCombineFramesOperation)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRegistrationEnabled)));
            }
        }
    }

    public TextComboBoxItem SelectedVideoFormat
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
                if (value == VideoFormats[CopyVideoFormatIndex])
                {
                    IsLocalContrastChecked = false;
                    CombineFramesMode = CombineFramesMode.None;
                }
            }
        }
    }
    TextComboBoxItem _selectedVideoFormat;

    public ObservableCollection<TextComboBoxItem> VideoFormats { get; } = [
        new TextComboBoxItem { Text = "Default" },
        new TextComboBoxItem { Text = "Copy", Tag = "-c copy" },
        new TextComboBoxItem { Text = "libx264", Tag = "-c:v libx264 -pix_fmt yuv420p" },
        new TextComboBoxItem { Text = "libx265", Tag = "-c:v libx265 -pix_fmt yuv420p" },
        new TextComboBoxItem { Text = "libvpx-vp9", Tag = "-c:v libvpx-vp9 -pix_fmt yuv420p" },
        new TextComboBoxItem { Text = "libsvtav1", Tag = "-c:v libsvtav1 -pix_fmt yuv420p" },
    ];

    const int DefaultVideoFormatIndex = 3;
    const int CopyVideoFormatIndex = 1;

    public string FrameRate
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
            {
                UpdateInputArgs();
                UpdateProcessArgs();
                UpdateOutputArgs();
            }
        }
    } = string.Empty;

    public string VideoBitRate
    {
        get;
        set
        {
            if (SetProperty(ref field, value.Trim()))
                UpdateOutputArgs();
        }
    } = string.Empty;

    public bool IsRemoveAudioChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                UpdateOutputArgs();
        }
    }

    public bool IsCombineFramesOperation => CombineFramesMode > CombineFramesMode.None || OutputMode is OutputMode.Average or OutputMode.Max or OutputMode.TimeSliceImage;

    public bool IsRegistrationEnabled => IsCombineFramesOperation && RegistrationMode > RegistrationMode.Off;

    public RegistrationMode RegistrationMode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRegistrationEnabled)));
        }
    }

    public string RegistrationRegion
    {
        get;
        set => SetProperty(ref field, value.Replace(" ", "", StringComparison.Ordinal));
    } = "w:h:x:y";

    CombineFramesRegistration? ParseRegistrationSettings()
    {
        if (RegistrationMode == RegistrationMode.Off)
            return null;
        
        ROI? roi = null;
        if (!string.IsNullOrEmpty(RegistrationRegion) && RegistrationRegion[0] != 'w')
        {
            var parts = RegistrationRegion.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 4
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out var w)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.CurrentCulture, out var h)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.CurrentCulture, out var x)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.CurrentCulture, out var y)
                || w <= 0 || h <= 0 || x < 0 || y < 0)
                throw new UserMessageException("Registration region must be width:height:left:top with positive size.");
            roi = new ROI { Left = x, Top = y, Width = w, Height = h };
        }
        return new CombineFramesRegistration(
            RegistrationMode == RegistrationMode.ToFirst ? RegistrationOperation.Reference.First : RegistrationOperation.Reference.Previous, roi);
    }

    public string DarkFramePath
    {
        get;
        set => SetProperty(ref field, value.TrimPath());
    } = string.Empty;

    public ICommand BrowseDarkFrameCommand => new RelayCommand(o =>
    {
        var dlg = new OpenFileDialog { Filter = OpenImageFileFilter };
        if (File.Exists(DarkFramePath))
            dlg.FileName = DarkFramePath;
        if (dlg.ShowDialog() is true)
            DarkFramePath = dlg.FileName;
    });

    public string InputArguments
    {
        get;
        set => SetProperty(ref field, value.Trim());
    } = string.Empty;

    public string ProcessArguments
    {
        get;
        set => SetProperty(ref field, value.Trim());
    } = string.Empty;

    public string OutputArguments
    {
        get;
        set => SetProperty(ref field, value.Trim());
    } = string.Empty;

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
        if (IsStabilizeChecked)
            filters.Add($"vidstabtransform=smoothing={SmoothFrames}"
                + (IsTripodChecked ? ":tripod=1" : null)
                + (IsBicubicStabilizeChecked ? ":interpol=bicubic" : null));
        if (IsScaleChecked && SelectedEffect.Text != ZoomEffect && SelectedEffect.Text != Crossfade)
            filters.Add($"scale={ScaleTo}");
        if (SelectedEffect.Tag is string effect)
            filters.Add(effect);
        else if (SelectedEffect.Tag is ParameterizedFilter effectFilter)
            filters.Add(string.Format(CultureInfo.InvariantCulture, effectFilter.Filter,
                EffectParameter, 
                IsScaleChecked ? ScaleTo.Replace(':', 'x') : "1920x1080",
                string.IsNullOrEmpty(FrameRate) ? "30" : FrameRate));
        if (IsSpeedupChecked && (CombineFramesMode != CombineFramesMode.RollingAverage || !SpeedupByEqualsCombineFramesCount))
            filters.Add($"setpts=PTS/({SpeedupBy})");
        if (!string.IsNullOrEmpty(FrameRate) && SelectedEffect.Text != ZoomEffect)
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
        if (OutputMode is OutputMode.Video)
        {
            var args = new List<string>();
            if (IsRemoveAudioChecked)
                args.Add("-an");
            else if (IncludeAudioStream)
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
            else if (SelectedVideoFormat.Tag is string tag && tag.StartsWith("-c:v", StringComparison.Ordinal))
                args.Add("-crf 23"); // Lower values give better quality
            OutputArguments = string.Join(" ", args);
        }
        else
            OutputArguments = string.Empty;
    }

    bool IncludeAudioStream => (IsLocalContrastChecked || CombineFramesMode > CombineFramesMode.None) && HasSingleInput && !HasOnlyImageInput;

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
            if (!IsAnyProcessingSelected())
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

    private bool IsAnyProcessingSelected()
    {
        return IsCropChecked || IsScaleChecked || IsRotateChecked || IsStabilizeChecked || IsSpeedupChecked || IsLocalContrastChecked || 
            IsCombineFramesOperation || SelectedEffect?.Tag is not null;
    }

    public ICommand CombineFade => new RelayCommand(async parameter =>
    {
        // Based on https://stackoverflow.com/questions/63553906/merging-multiple-video-files-with-ffmpeg-and-xfade-filter

        const double FadeDuration = 1;
        const string Transition = "fade"; // See https://ffmpeg.org/ffmpeg-filters.html#xfade

        if (string.IsNullOrEmpty(_mainViewModel.Settings.ExifToolPath))
            throw new UserMessageException("ExifTool path is not set in settings, please set it before using this command");
        var allSelected = _mainViewModel.GetSelectedItems(true).Where(item => item.IsVideo).ToArray();
        if (allSelected.Length < 2)
            throw new UserMessageException("Select at least 2 videos");

        if (parameter is not string outFileName)
        {
            var dlg = new SaveFileDialog();
            dlg.InitialDirectory = Path.GetDirectoryName(allSelected[0].FullPath);
            dlg.FileName = Path.GetFileNameWithoutExtension(allSelected[0].Name) + "[combined].mp4";
            dlg.Filter = SaveVideoFilter;
            dlg.CheckPathExists = false;
            dlg.DefaultExt = ".mp4";
            if (dlg.ShowDialog() is not true)
                return;
            outFileName = dlg.FileName;
        }

        await using var pause = _mainViewModel.PauseFileSystemWatcher();
        await _mainViewModel.RunProcessWithProgressBarAsync(async (progressCallback, ct) =>
        {
            progressCallback(-1);
            var clipDurations = new double[allSelected.Length];
            for (int i = 0; i < allSelected.Length; i++)
            {
                var metadata = ExifTool.LoadMetadata(allSelected[i].FullPath, _mainViewModel.Settings.ExifToolPath);
                var spanStr = metadata["Duration"];
                if (!double.TryParse(spanStr.Trim('s'), CultureInfo.InvariantCulture, out clipDurations[i]))
                    clipDurations[i] = TimeSpan.Parse(spanStr, CultureInfo.InvariantCulture).TotalSeconds;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < allSelected.Length; i++)
                sb.Append("-i \"").Append(allSelected[i].FullPath).Append("\" ");
            sb.Append("-filter_complex \"");
            double offset = 0;
            for (int i = 0; i < allSelected.Length - 1; i++)
            {
                offset += clipDurations[i] - FadeDuration;
                if (i == 0)
                    sb.Append("[0]");
                else
                    sb.Append(CultureInfo.InvariantCulture, $"[vfade{i}]");
                sb.Append(CultureInfo.InvariantCulture, $"[{i + 1}:v]xfade=transition={Transition}:duration={FadeDuration}:offset={offset}");

                if (i < allSelected.Length - 2)
                    sb.Append(CultureInfo.InvariantCulture, $"[vfade{i + 1}]; ");
            }
            sb.Append(", format=yuv420p\" ");
            sb.Append(OutputArguments);
            sb.Append(CultureInfo.InvariantCulture, $" -y \"{outFileName}\"");

            PrepareProgressDisplay(progressCallback);
            _inputDuration = TimeSpan.FromSeconds(clipDurations.Sum() - (allSelected.Length - 1) * FadeDuration);
            _hasDuration = true;

            Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
            await _videoTransforms.RunFFmpegAsync(sb.ToString(), ProcessStdError, ct);
        }, "Processing...");
        await _mainViewModel.AddOrUpdateItemAsync(outFileName, false, true);
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
        if (dlg.ShowDialog() is not true)
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
    public ICommand GenerateTimeSliceVideo => new RelayCommand(o =>
    {
        CombineFramesMode = CombineFramesMode.TimeSlice;
        ProcessSelected.Execute(null);
    });

    internal void CropSelected(Rect cropRectangle)
    {
        var w = IntMath.Round(cropRectangle.Width) & ~3;
        var h = IntMath.Round(cropRectangle.Height) & ~3;
        if (w == 0 || h == 0)
            throw new UserMessageException("Crop size is too small");
        var x = IntMath.Round(cropRectangle.X + (cropRectangle.Width - w) / 2);
        var y = IntMath.Round(cropRectangle.Y + (cropRectangle.Height - h) / 2);
        _cropWindow = string.Create(CultureInfo.InvariantCulture, $"{w}:{h}:{x}:{y}");
        IsCropChecked = true;
        ProcessSelected.Execute(null);
    }

    private string? ShowSetupUserInterface(PictureItemViewModel[] allSelected)
    {
        if (HasSingleInput && !string.IsNullOrEmpty(SkipTo))
            _mainViewModel.UpdatePreviewPictureAsync(SkipTo).WithExceptionLogging();
        if (_localContrastSetup is not null && _localContrastSetup.SourceBitmap is not null)
            _localContrastSetup.SourceBitmap = null;
        var window = new VideoTransformWindow() { Owner = App.Current.MainWindow, DataContext = this };
        try
        {
            if (window.ShowDialog() is not true)
                return null;
        }
        finally
        {
            if (_localContrastSetup is not null && _localContrastSetup.SourceBitmap is not null)
                _localContrastSetup.SourceBitmap = null;
            window.DataContext = null;
        }

        var inPath = Path.GetDirectoryName(allSelected[0].FullPath)!;
        if (OutputMode == OutputMode.ImageSequence)
        {
            var outputPath = TextInputWindow.Show($"Output path:", text => !string.IsNullOrWhiteSpace(text), "Extract frames",
                Path.Combine(inPath, Path.GetFileNameWithoutExtension(allSelected[0].Name), "%06d.jpg"));
            return outputPath;
        }
        if (OutputMode == OutputMode.Video && SelectedVideoFormat == VideoFormats[CopyVideoFormatIndex] && IsAnyProcessingSelected())
            throw new UserMessageException("Copy video format is not supported with any processing selected, please select another format or disable processing options");
        var dlg = SetupSaveFileDialog(allSelected, inPath);
        if (dlg.ShowDialog() is not true)
            return null;
        return dlg.FileName;
    }

    public ICommand ProcessSelected => new RelayCommand(async parameter =>
    {
        var allSelected = UpdateInputArgs();
        if (!IsRemoveAudioChecked && IncludeAudioStream)
            UpdateOutputArgs();
        if (parameter is not string outFileName)
        {
            var outFileNameFromUi = ShowSetupUserInterface(allSelected);
            if (outFileNameFromUi is null)
                return;
            outFileName = outFileNameFromUi;
        }

        await using var pause = _mainViewModel.PauseFileSystemWatcher();
        string? message = null;
        var selectedTimeSliceDirection = SelectedTimeSliceDirection.Tag;
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

            Directory.SetCurrentDirectory(Path.GetDirectoryName(allSelected[0].FullPath)!);
            if (allSelected.Length > 1)
                await File.WriteAllLinesAsync(InputListFileName, allSelected.Select(f => $"file '{f.Name}'"), ct).ConfigureAwait(false);

            if (IsStabilizeChecked)
                await RunStabilizePreProcessingAsync(ct).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(outFileName)!);
            if (OutputMode is OutputMode.Average)
                message = await RunAverageProcessingAsync(outFileName, sw, ct).ConfigureAwait(false);
            else if (OutputMode is OutputMode.Max)
                message = await RunMaxProcessingAsync(outFileName, sw, ct).ConfigureAwait(false);
            else if (CombineFramesMode is CombineFramesMode.TimeSlice or CombineFramesMode.TimeSliceInterpolated)
                message = await RunTimeSliceProcessingAsync(outFileName, selectedTimeSliceDirection!, sw, ct).ConfigureAwait(false);
            else if (IsLocalContrastChecked || CombineFramesMode > CombineFramesMode.None)
                await RunLocalFrameProcessingAsync(outFileName, ct).ConfigureAwait(false);
            else
                await _videoTransforms.RunFFmpegAsync($"{InputArguments} {ProcessArguments} {OutputArguments} -y \"{outFileName}\"", ProcessStdError, ct).ConfigureAwait(false);
            _progressCallback?.Invoke(1);
            if (allSelected.Length > 1)
                File.Delete(InputListFileName);
            if (IsStabilizeChecked)
                File.Delete(TransformsFileName);
        }, "Processing...");
        if (OutputMode == OutputMode.ImageSequence)
            outFileName = Path.GetDirectoryName(outFileName)!;
        if (string.Equals(Path.GetDirectoryName(outFileName), Path.GetDirectoryName(_mainViewModel.SelectedItem?.FullPath), StringComparison.CurrentCultureIgnoreCase))
            await _mainViewModel.AddOrUpdateItemAsync(outFileName, OutputMode == OutputMode.ImageSequence, true);
        if (!string.IsNullOrEmpty(message) && parameter is null)
            MessageBox.Show(App.Current.MainWindow, message);
    });      

    async Task RunStabilizePreProcessingAsync(CancellationToken ct)
    {
        _progressScale = 0.5;
        File.Delete(TransformsFileName);
        await _videoTransforms.RunFFmpegAsync($"{InputArguments} {StabilizeArguments}", ProcessStdError, ct).ConfigureAwait(false);
        _progressOffset = 0.5;
    }

    async Task<string> RunAverageProcessingAsync(string outFileName, Stopwatch sw, CancellationToken ct)
    {
        using var process = new AverageFramesOperation(DarkFramePath, ParseRegistrationSettings(), ct);
        await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync($"{InputArguments} {ProcessArguments}", process.ProcessImage, ProcessStdError, ct).ConfigureAwait(false);
        if (process.Supports16BitResult() && Path.GetExtension(outFileName).ToUpperInvariant() is ".PNG" or ".TIF" or ".TIFF" or ".JXR")
            GeneralFileFormatHandler.SaveToFile(process.GetResult16(), outFileName, CreateImageMetadata());
        else
            GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata(), _mainViewModel.Settings.JpegQuality);
        return $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
    }

    async Task<string> RunMaxProcessingAsync(string outFileName, Stopwatch sw, CancellationToken ct)
    {
        using var process = new MaxFramesOperation(DarkFramePath, ParseRegistrationSettings(), ct);
        await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync($"{InputArguments} {ProcessArguments}", process.ProcessImage, ProcessStdError, ct).ConfigureAwait(false);
        GeneralFileFormatHandler.SaveToFile(process.GetResult8(), outFileName, CreateImageMetadata(), _mainViewModel.Settings.JpegQuality);
        return $"Processed {process.ProcessedImages} frames in {sw.Elapsed.TotalSeconds:N1}s";
    }

    async Task<string> RunTimeSliceProcessingAsync(string outFileName, object selectedTimeSliceDirection, Stopwatch sw, CancellationToken ct)
    {
        var timeSlice = new TimeSliceOperation();
        if (selectedTimeSliceDirection is FloatBitmap selectionMap)
            timeSlice.SelectionMap = selectionMap;
        else
            timeSlice.SelectionMapExpression = (SelectionMapFunction)selectedTimeSliceDirection;

        using var runningAverage = (RegistrationMode > RegistrationMode.Off || !string.IsNullOrEmpty(DarkFramePath)) ?
            new RollingAverageOperation(1, DarkFramePath, ParseRegistrationSettings(), ct) : null;

        if (!string.IsNullOrEmpty(FrameRate))
        {
            _fps = double.Parse(FrameRate, CultureInfo.InvariantCulture);
            _hasFps = true;
        }
        if (OutputMode is OutputMode.Video)
            _progressScale = 0.5;
        await _videoTransforms.RunFFmpegWithStreamOutputImagesAsync($"{InputArguments} {ProcessArguments}", frame =>
        {
            if (runningAverage is not null)
            {
                runningAverage.ProcessImage(frame);
                frame = runningAverage.GetResult8();
            }
            if (IsLocalContrastChecked && !_localContrastSetup.IsNoOperation)
                frame = _localContrastSetup.ApplyOperations(frame);
            timeSlice.AddFrame(frame);
        }, ProcessStdError, ct).ConfigureAwait(false);

        if (OutputMode is OutputMode.TimeSliceImage)
        {
            var timeSliceImage = CombineFramesMode == CombineFramesMode.TimeSliceInterpolated
                ? timeSlice.GenerateTimeSliceImageInterpolated()
                : timeSlice.GenerateTimeSliceImage();
            GeneralFileFormatHandler.SaveToFile(timeSliceImage, outFileName, CreateImageMetadata(), _mainViewModel.Settings.JpegQuality);
        }
        else
        {
            if (!_hasFps && OutputMode != OutputMode.ImageSequence)
                throw new UserMessageException("Unable to determine frame rate, please specify manually");
            _progressOffset = 0.5;
            var frames = CombineFramesMode == CombineFramesMode.TimeSliceInterpolated
                ? timeSlice.GenerateTimeSliceVideoInterpolated(CombineFramesCount)
                : timeSlice.GenerateTimeSliceVideo(CombineFramesCount);
            await _videoTransforms.RunFFmpegWithStreamInputImagesAsync(_hasFps ? _fps : null, $"{OutputArguments} -y \"{outFileName}\"", frames, ProcessStdError, ct).ConfigureAwait(false);
        }
        return $"Processed {timeSlice.UsedFrames} frames and skipped {timeSlice.SkippedFrames} in {sw.Elapsed.TotalSeconds:N1}s.\n" +
            "If frames are skipped it means that the video is too big to load into memory. To reduce the number of frames loaded, " +
            "you can set 'Speedup by' to e.g. 4 to only use every 4th frame. You can also reduce the resolution using 'Scale to'.";
    }

    async Task RunLocalFrameProcessingAsync(string outFileName, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(FrameRate))
        {
            _fps = double.Parse(FrameRate, CultureInfo.InvariantCulture);
            _hasFps = true;
        }
        using CombineFramesOperationBase? runningAverage = CombineFramesMode switch
        {
            CombineFramesMode.RollingAverage => IsSpeedupChecked && SpeedupByEqualsCombineFramesCount ?
                new TimeCompressionAverageOperation(CombineFramesCount, DarkFramePath, ParseRegistrationSettings(), ct) :
                new RollingAverageOperation(CombineFramesCount, DarkFramePath, ParseRegistrationSettings(), ct),
            CombineFramesMode.FadingAverage => new FadingAverageOperation(CombineFramesCount, DarkFramePath, ParseRegistrationSettings(), ct),
            CombineFramesMode.FadingMax => new FadingMaxOperation(CombineFramesCount, DarkFramePath, ParseRegistrationSettings(), ct),
            _ => null,
        };

        using var frameEnumerator = new QueueEnumerable<BitmapSource>();
        var readTask = _videoTransforms.RunFFmpegWithStreamOutputImagesAsync($"{InputArguments} {ProcessArguments}",
            source =>
            {
                if (runningAverage is not null)
                {
                    runningAverage.ProcessImage(source);
                    if (!runningAverage.IsResultReady)
                        return;
                    if (!IsLocalContrastChecked || _localContrastSetup.IsNoOperation)
                    {
                        frameEnumerator.AddItem(runningAverage.GetResult8());
                        return;
                    }
                    source = runningAverage.GetResult16();
                }
                frameEnumerator.AddItem(_localContrastSetup!.ApplyOperations(source));
            }, ProcessStdError, ct);
        await Task.WhenAny(frameEnumerator.GotFirst, Task.Delay(TimeSpan.FromSeconds(10), ct)).ConfigureAwait(false);
        if (!_hasFps && OutputMode != OutputMode.ImageSequence)
            throw new UserMessageException("Unable to determine frame rate, please specify manually");
        var writeTask = _videoTransforms.RunFFmpegWithStreamInputImagesAsync(_hasFps ? _fps : null, $"{OutputArguments} -y \"{outFileName}\"", frameEnumerator,
            stdError => Log.Write("Writer: " + stdError), ct);
        await await Task.WhenAny(readTask, writeTask).ConfigureAwait(false); // Write task is not expected to finish here, only if it fails
        frameEnumerator.Break();
        await writeTask.ConfigureAwait(false);
    }

    bool SpeedupByEqualsCombineFramesCount => int.TryParse(SpeedupBy, CultureInfo.InvariantCulture, out var speedupBy) && speedupBy == CombineFramesCount;

    SaveFileDialog SetupSaveFileDialog(PictureItemViewModel[] allSelected, string inPath)
    {
        string postfix, ext;
        var dlg = new SaveFileDialog();
        switch (OutputMode)
        {
            case OutputMode.Video:
                if (CombineFramesMode is CombineFramesMode.TimeSlice or CombineFramesMode.TimeSliceInterpolated)
                    postfix = "timeslice";
                else if (CombineFramesMode == CombineFramesMode.RollingAverage && CombineFramesCount > 1)
                    postfix = "rolling" + CombineFramesCount;
                else if (CombineFramesMode == CombineFramesMode.FadingAverage && CombineFramesCount > 1)
                    postfix = "fadeavg" + CombineFramesCount;
                else if (CombineFramesMode == CombineFramesMode.FadingMax && CombineFramesCount > 1)
                    postfix = "fademax" + CombineFramesCount;
                else if (IsStabilizeChecked)
                    postfix = "stabilized" + SmoothFrames;
                else if (RegistrationMode > RegistrationMode.Off && CombineFramesMode > CombineFramesMode.None)
                    postfix = "stabilized";
                else if (allSelected.Length > 1)
                    postfix = "combined";
                else if (SelectedEffect.Tag is not null)
                    postfix = SelectedEffect.Text.Split(' ')[0].ToLowerInvariant();
                else if (SelectedVideoFormat == VideoFormats[CopyVideoFormatIndex] && (!string.IsNullOrEmpty(SkipTo) || !string.IsNullOrEmpty(Duration)))
                    postfix = "trim";
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
            case OutputMode.TimeSliceImage:
                postfix = "timeslice";
                dlg.Filter = GeneralFileFormatHandler.SaveImageFilter;
                dlg.FilterIndex = 1;
                ext = ".jpg";
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

    BitmapMetadata? CreateImageMetadata()
    {
        var firstSelected = _mainViewModel.GetSelectedItems(true).First();
        return ExifHandler.EncodePngMetadata(
            _hasDuration && _inputDuration.TotalSeconds >= 0.99 ? new Rational(IntMath.Round(_inputDuration.TotalSeconds), 1) : null,
            firstSelected.Location,
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
