using Microsoft.Win32;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PhotoLocator
{
    class LocalContrastViewModel : INotifyPropertyChanged, IImageZoomPreviewViewModel
    {
        enum FirstParamChanged { Astro, LaplacianPyramid, LocalContrast, ColorTone, None }

        static AdjustmentValues? _adjustmentClipboard;
        static AdjustmentValues? _lastUsedValues;
        readonly DispatcherTimer _updateTimer;
        readonly LaplacianFilterOperation _laplacianFilterOperation = new();
        readonly IncreaseLocalContrastOperation _localContrastOperation = new() { DstBitmap = new() };
        readonly ColorToneAdjustOperation _colorToneOperation = new();
        Task _previewTask = Task.CompletedTask;
        FirstParamChanged _firstParamChanged = FirstParamChanged.None;

        public LocalContrastViewModel()
        {
            _updateTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background,
                async (s, e) =>
                {
                    try
                    {
                        await UpdatePreviewAsync();
                    }
                    catch (Exception ex)
                    {
                        _previewTask = Task.CompletedTask;
                        Mouse.OverrideCursor = null;
                        ExceptionHandler.ShowException(ex);
                    }
                },
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) { IsEnabled = false };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            StartUpdateTimer(FirstParamChanged.ColorTone);
        }

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public string FileName { get; set; } = string.Empty;

        public BitmapSource? SourceBitmap
        {
            get;
            set
            {
                _previewPictureSource = value;
                _laplacianFilterOperation.SourceChanged();
                _localContrastOperation.SourceChanged();
                _colorToneOperation.SourceChanged();
                if (value is not null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;
                    _sourceFloatBitmap.Assign(value, FloatBitmap.DefaultMonitorGamma);
                    if (IsAstroModeEnabled)
                        ResetCommand.Execute(null);
                    _updateTimer.Start();
                }
                field = value;
            }
        }
        readonly FloatBitmap _sourceFloatBitmap = new();

        public BitmapSource? PreviewPictureSource
        {
            get => _previewPictureSource;
            set => SetProperty(ref _previewPictureSource, value);
        }
        private BitmapSource? _previewPictureSource;

        public int PreviewZoom
        {
            get;
            set => SetProperty(ref field, value);
        }

        public ICommand ToggleZoomCommand => new RelayCommand(o => PreviewZoom = PreviewZoom > 0 ? 0 : 1);
        public ICommand ZoomToFitCommand => new RelayCommand(o => PreviewZoom = 0);
        public ICommand Zoom100Command => new RelayCommand(o => PreviewZoom = 1);
        public ICommand Zoom200Command => new RelayCommand(o => PreviewZoom = 2);
        public ICommand Zoom400Command => new RelayCommand(o => PreviewZoom = 4);
        public ICommand ZoomInCommand => new RelayCommand(o => PreviewZoom = Math.Min(PreviewZoom + 1, 4));
        public ICommand ZoomOutCommand => new RelayCommand(o => PreviewZoom = Math.Max(PreviewZoom - 1, 0));

        public bool IsAstroModeEnabled
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool IsLocalAdjustmentsLabelVisible => !IsAstroModeEnabled;

        public double AstroStretch
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.Astro);
            }
        }
        public ICommand ResetAstroStretchCommand => new RelayCommand(o => AstroStretch = IsAstroModeEnabled ? AstroStretchOperation.OptimizeStretch(_sourceFloatBitmap) : 0);

        public const double DefaultBackgroundRemovalSmooth = 8;
        public double BackgroundRemovalSmooth
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.Astro);
            }
        } = DefaultBackgroundRemovalSmooth;
        public ICommand ResetBackgroundRemovalSmoothCommand => new RelayCommand(o => BackgroundRemovalSmooth = DefaultBackgroundRemovalSmooth);

        public double BlackPoint
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.Astro);
            }
        }
        public ICommand ResetBlackPointCommand => new RelayCommand(o => BlackPoint = 0);

        public const double DefaultHighlightStrength = 10;
        public double HighlightStrength
        {
            get;
            set
            {
                if (SetProperty(ref field, RealMath.Clamp(value, 0, 100)))
                    StartUpdateTimer(FirstParamChanged.LocalContrast);
            }
        } = DefaultHighlightStrength;
        public ICommand ResetHighlightCommand => new RelayCommand(o => HighlightStrength = DefaultHighlightStrength);

        public const double DefaultShadowStrength = 10;
        public double ShadowStrength
        {
            get;
            set
            {
                if (SetProperty(ref field, RealMath.Clamp(value, 0, 100)))
                    StartUpdateTimer(FirstParamChanged.LocalContrast);
            }
        } = DefaultShadowStrength;
        public ICommand ResetShadowCommand => new RelayCommand(o => ShadowStrength = DefaultShadowStrength);

        public const double DefaultMaxStretch = 50;
        public double MaxStretch
        {
            get;
            set
            {
                if (SetProperty(ref field, RealMath.Clamp(value, 0, 100)))
                    StartUpdateTimer(FirstParamChanged.LocalContrast);
            }
        } = DefaultMaxStretch;
        public ICommand ResetMaxStretchCommand => new RelayCommand(o => MaxStretch = ToneMapping > 1 || IsAstroModeEnabled ? 100 : DefaultMaxStretch);

        public const double DefaultOutlierReductionStrength = 10;
        public double OutlierReductionStrength
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.LocalContrast);
            }
        } = DefaultOutlierReductionStrength;
        public ICommand ResetOutlierReductionCommand => new RelayCommand(o => OutlierReductionStrength = DefaultOutlierReductionStrength);

        public const double DefaultContrast = 1;
        public double Contrast
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.LocalContrast);
            }
        } = DefaultContrast;
        public ICommand ResetContrastCommand => new RelayCommand(o => Contrast = DefaultContrast);

        public const double DefaultToneMapping = 1;
        public double ToneMapping
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                {
                    if (value > 1)
                        MaxStretch = 100;
                    StartUpdateTimer(FirstParamChanged.LaplacianPyramid);
                }
            }
        } = DefaultToneMapping;
        public ICommand ResetToneMappingCommand => new RelayCommand(o => ToneMapping = DefaultToneMapping);

        public const double DefaultDetailHandling = 1;
        public double DetailHandling
        {
            get;
            set
            {
                if (SetProperty(ref field, value))
                    StartUpdateTimer(FirstParamChanged.LaplacianPyramid);
            }
        } = DefaultDetailHandling;
        public ICommand ResetDetailHandlingCommand => new RelayCommand(o => DetailHandling = DefaultDetailHandling);

        public ColorToneAdjustOperation.ToneAdjustment[] ToneAdjustments => _colorToneOperation.ToneAdjustments;

        public IEnumerable<ComboBoxItem> ColorTones
        {
            get
            {
                UpdateColorTones();
                for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
                {
                    yield return new ComboBoxItem { Content = _colorTones[i] };
                }
                yield return new ComboBoxItem { Content = "All" };
            }
        }

        private void UpdateColorTones()
        {
            for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
            {
                _colorTones[i] ??= new Rectangle() { Width = 35, Height = 12, RadiusX = 4, RadiusY = 4, Stroke = Brushes.Black };
                ColorToneAdjustOperation.ColorTransformHSI2RGB(_colorToneOperation.ToneAdjustments[i].ToneHue + (float)ToneRotation, 0.8f, 0.5f,
                    out var r, out var g, out var b);
                _colorTones[i].Fill = new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
            }
        }
        readonly Rectangle[] _colorTones = new Rectangle[ColorToneAdjustOperation.NumberOfTones];

        public int ActiveToneIndex
        {
            get;
            set
            {
                if (SetProperty(ref field, value) && value >= 0)
                {
                    if (value == ColorToneAdjustOperation.NumberOfTones)
                    {
                        for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones - 1; i++)
                        {
                            _colorToneOperation.ToneAdjustments[i].AdjustHue = _colorToneOperation.ToneAdjustments[ColorToneAdjustOperation.NumberOfTones - 1].AdjustHue;
                            _colorToneOperation.ToneAdjustments[i].AdjustSaturation = _colorToneOperation.ToneAdjustments[ColorToneAdjustOperation.NumberOfTones - 1].AdjustSaturation;
                            _colorToneOperation.ToneAdjustments[i].AdjustIntensity = _colorToneOperation.ToneAdjustments[ColorToneAdjustOperation.NumberOfTones - 1].AdjustIntensity;
                            _colorToneOperation.ToneAdjustments[i].HueUniformity = _colorToneOperation.ToneAdjustments[ColorToneAdjustOperation.NumberOfTones - 1].HueUniformity;
                        }
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HueAdjust)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaturationAdjust)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntensityAdjust)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HueUniformity)));
                }
            }
        } = ColorToneAdjustOperation.NumberOfTones;

        public float HueAdjust
        {
            get => _colorToneOperation.ToneAdjustments[Math.Min(ActiveToneIndex, ColorToneAdjustOperation.NumberOfTones - 1)].AdjustHue;
            set
            {
                if (ActiveToneIndex == ColorToneAdjustOperation.NumberOfTones)
                {
                    for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
                        _colorToneOperation.ToneAdjustments[i].AdjustHue = value;
                    NotifyPropertyChanged();
                }
                else if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustHue, value))
                    StartUpdateTimer(FirstParamChanged.ColorTone);
            }
        }

        public ICommand ResetHueAdjustCommand => new RelayCommand(o => HueAdjust = 0);

        public float SaturationAdjust
        {
            get => _colorToneOperation.ToneAdjustments[Math.Min(ActiveToneIndex, ColorToneAdjustOperation.NumberOfTones - 1)].AdjustSaturation;
            set
            {
                if (ActiveToneIndex == ColorToneAdjustOperation.NumberOfTones)
                {
                    for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
                        _colorToneOperation.ToneAdjustments[i].AdjustSaturation = value;
                    NotifyPropertyChanged();
                }
                else if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustSaturation, value))
                    StartUpdateTimer(FirstParamChanged.ColorTone);
            }
        }

        public ICommand ResetSaturationAdjustCommand => new RelayCommand(o => SaturationAdjust = 1);

        public float IntensityAdjust
        {
            get => _colorToneOperation.ToneAdjustments[Math.Min(ActiveToneIndex, ColorToneAdjustOperation.NumberOfTones - 1)].AdjustIntensity;
            set
            {
                if (ActiveToneIndex == ColorToneAdjustOperation.NumberOfTones)
                {
                    for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
                        _colorToneOperation.ToneAdjustments[i].AdjustIntensity = value;
                    NotifyPropertyChanged();
                }
                else if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustIntensity, value))
                    StartUpdateTimer(FirstParamChanged.ColorTone);
            }
        }

        public ICommand ResetIntensityAdjustCommand => new RelayCommand(o => IntensityAdjust = 1);

        public float HueUniformity
        {
            get => _colorToneOperation.ToneAdjustments[Math.Min(ActiveToneIndex, ColorToneAdjustOperation.NumberOfTones - 1)].HueUniformity;
            set
            {
                if (ActiveToneIndex == ColorToneAdjustOperation.NumberOfTones)
                {
                    for (int i = 0; i < ColorToneAdjustOperation.NumberOfTones; i++)
                        _colorToneOperation.ToneAdjustments[i].HueUniformity = value;
                    NotifyPropertyChanged();
                }
                else if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].HueUniformity, value))
                    StartUpdateTimer(FirstParamChanged.ColorTone);
            }
        }

        public ICommand ResetHueUniformityCommand => new RelayCommand(o => HueUniformity = 0);

        public double ToneRotation
        {
            get;
            set
            {
                if (SetProperty(ref field, RealMath.Clamp(value, -0.5, 0.5)))
                {
                    UpdateColorTones();
                    StartUpdateTimer(FirstParamChanged.ColorTone);
                }
            }
        }

        public ICommand ResetToneRotationCommand => new RelayCommand(o => ToneRotation = 0);

        public ICommand ShowOriginalCommand => new RelayCommand(o =>
        {
            PreviewPictureSource = SourceBitmap;
        });

        public ICommand ResetCommand => new RelayCommand(o =>
        {
            ResetAstroStretchCommand.Execute(null);
            BackgroundRemovalSmooth = DefaultBackgroundRemovalSmooth;
            BlackPoint = 0;
            HighlightStrength = DefaultHighlightStrength;
            ShadowStrength = DefaultShadowStrength;
            OutlierReductionStrength = DefaultOutlierReductionStrength;
            Contrast = DefaultContrast;
            ToneMapping = DefaultToneMapping;
            DetailHandling = DefaultDetailHandling;
            ResetMaxStretchCommand.Execute(null);
            _colorToneOperation.ResetToneAdjustments();
            ToneRotation = 0;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = ColorToneAdjustOperation.NumberOfTones;
            StartUpdateTimer(FirstParamChanged.ColorTone);
        });

        public ICommand CopyAdjustmentsCommand => new RelayCommand(o => _adjustmentClipboard = StoreAdjustmentValues());

        public ICommand PasteAdjustmentsCommand => new RelayCommand(o => RestoreAdjustmentValues(_adjustmentClipboard ?? throw new UserMessageException("No adjustments saved")), o => _adjustmentClipboard is not null);

        public ICommand RestoreLastUsedValuesCommand => new RelayCommand(o => RestoreAdjustmentValues(_lastUsedValues ?? throw new UserMessageException("No adjustments saved")), o => _lastUsedValues is not null);

        public ICommand SaveAdjustmentsCommand => new RelayCommand(o =>
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save adjustments",
                Filter = "Adjustment values (*.pla)|*.pla",
                DefaultExt = "pla",
                FileName = FileName + ".pla"
            };
            if (dlg.ShowDialog() is true)
                StoreAdjustmentValues().SaveToJsonFile(dlg.FileName);
        });

        public ICommand LoadAdjustmentsCommand => new RelayCommand(o =>
        {
            var dlg = new OpenFileDialog
            {
                Title = "Load adjustments",
                Filter = "Adjustment values (*.pla)|*.pla"
            };
            if (dlg.ShowDialog() is true)
                RestoreAdjustmentValues(AdjustmentValues.LoadFromJsonFile(dlg.FileName));
        });

        public void TryLoadAdjustments()
        {
            var adjustmentsFileName = FileName + ".pla";
            if (File.Exists(adjustmentsFileName))
                try
                {
                    RestoreAdjustmentValues(AdjustmentValues.LoadFromJsonFile(adjustmentsFileName));
                }
                catch (Exception ex)
                {
                    Log.Write($"Error loading adjustments from '{adjustmentsFileName}': {ex.Message}");
                }
        }

        public void SaveLastUsedValues()
        {
            _lastUsedValues = StoreAdjustmentValues();
        }

        private AdjustmentValues StoreAdjustmentValues()
        {
            var av = new AdjustmentValues();
            av.AstroStretch = AstroStretch;
            av.BackgroundRemovalSmooth = BackgroundRemovalSmooth;
            av.BlackPoint = BlackPoint;
            av.HighlightStrength = HighlightStrength;
            av.ShadowStrength = ShadowStrength;
            av.OutlierReductionStrength = OutlierReductionStrength;
            av.Contrast = Contrast;
            av.ToneMapping = ToneMapping;
            av.DetailHandling = DetailHandling;
            av.MaxStretch = MaxStretch;
            av.ToneRotation = ToneRotation;
            if (_colorToneOperation.AreToneAdjustmentsChanged)
            {
                av.ToneAdjustments = new ToneAdjustmentValues[_colorToneOperation.ToneAdjustments.Length];
                for (int i = 0; i < av.ToneAdjustments.Length; i++)
                {
                    var src = _colorToneOperation.ToneAdjustments[i];
                    var dst = av.ToneAdjustments[i] = new ToneAdjustmentValues();
                    dst.AdjustHue = src.AdjustHue;
                    dst.AdjustSaturation = src.AdjustSaturation;
                    dst.AdjustIntensity = src.AdjustIntensity;
                    dst.HueUniformity = src.HueUniformity;
                }
            }
            return av;
        }

        private void RestoreAdjustmentValues(AdjustmentValues av)
        {
            AstroStretch = av.AstroStretch;
            BackgroundRemovalSmooth = av.BackgroundRemovalSmooth;
            BlackPoint = av.BlackPoint;
            HighlightStrength = av.HighlightStrength;
            ShadowStrength = av.ShadowStrength;
            OutlierReductionStrength = av.OutlierReductionStrength;
            Contrast = av.Contrast;
            ToneMapping = av.ToneMapping;
            DetailHandling = av.DetailHandling;
            MaxStretch = av.MaxStretch;
            ToneRotation = av.ToneRotation;
            if (_colorToneOperation.ToneAdjustments.Length != av.ToneAdjustments?.Length)
                _colorToneOperation.ResetToneAdjustments();
            else
                for (int i = 0; i < av.ToneAdjustments.Length; i++)
                {
                    var src = av.ToneAdjustments[i];
                    _colorToneOperation.ToneAdjustments[i].AdjustHue = src?.AdjustHue ?? 0;
                    _colorToneOperation.ToneAdjustments[i].AdjustSaturation = src?.AdjustSaturation ?? 1;
                    _colorToneOperation.ToneAdjustments[i].AdjustIntensity = src?.AdjustIntensity ?? 1;
                    _colorToneOperation.ToneAdjustments[i].HueUniformity = src?.HueUniformity ?? 0;
                }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = _colorToneOperation.AreToneAdjustmentsChanged ? 0 : ColorToneAdjustOperation.NumberOfTones;
            StartUpdateTimer(FirstParamChanged.ColorTone);
        }

        private void StartUpdateTimer(FirstParamChanged firstParamChanged)
        {
            if (Application.Current is null)
                return;
            Mouse.OverrideCursor = Cursors.AppStarting;
            if (firstParamChanged < _firstParamChanged)
                _firstParamChanged = firstParamChanged;
            _updateTimer.Stop();
            _updateTimer.Start();
        }

        void ApplyAstroStretchOperation()
        {
            if (IsAstroModeEnabled && (AstroStretch > 0 || BackgroundRemovalSmooth > 0 || BlackPoint > 0))
            {
                var astroStretch = new AstroStretchOperation()
                {
                    SrcBitmap = _sourceFloatBitmap,
                    DstBitmap = new(),
                    Stretch = AstroStretch,
                    BackgroundSmooth = BackgroundRemovalSmooth,
                    BlackPoint = BlackPoint,
                };
                astroStretch.Apply();
                _laplacianFilterOperation.SourceChanged();
                _laplacianFilterOperation.SrcBitmap = astroStretch.DstBitmap;
            }
            else
                _laplacianFilterOperation.SrcBitmap = _sourceFloatBitmap;
        }

        private void ApplyLaplacianFilterOperation()
        {
            if (DetailHandling != 1 || ToneMapping != 1)
            {
                _laplacianFilterOperation.DstBitmap ??= new();
                _laplacianFilterOperation.Alpha = (float)Math.Max(0.01, 2 - DetailHandling);
                _laplacianFilterOperation.Beta = (float)(2 - ToneMapping);
                _laplacianFilterOperation.Apply();
                _localContrastOperation.SrcBitmap = _laplacianFilterOperation.DstBitmap;
            }
            else
                _localContrastOperation.SrcBitmap = _laplacianFilterOperation.SrcBitmap;
        }

        private void ApplyLocalContrastOperation()
        {
            _localContrastOperation.LocalMaxFilterSize = HighlightStrength == 0 ? float.NaN : (float)(_localContrastOperation.SrcBitmap.Width * (101 - HighlightStrength) / 100);
            _localContrastOperation.LocalMinFilterSize = ShadowStrength == 0 ? float.NaN : (float)(_localContrastOperation.SrcBitmap.Width * (101 - ShadowStrength) / 100);
            _localContrastOperation.OutlierReductionFilterSize = (float)(OutlierReductionStrength / 2);
            _localContrastOperation.MaxContrast = (float)(MaxStretch / 100);
            _localContrastOperation.Apply();
        }

        private void ApplyColorToneOperation()
        {
            if (!_colorToneOperation.AreToneAdjustmentsChanged)
                return;
            _colorToneOperation.SrcBitmap = _colorToneOperation.DstBitmap = _localContrastOperation.DstBitmap;
            _colorToneOperation.Rotation = (float)ToneRotation;
            _colorToneOperation.Apply();
        }

        public async Task UpdatePreviewAsync()
        {
            _updateTimer.Stop();
            await _previewTask;
            if (!_previewTask.IsCompleted) // We might have multiple instance of UpdatePreviewAsync running at the same time
            {
                await _previewTask;
                return;
            }
            await (_previewTask = Task.Run(async () =>
            {
                if (_firstParamChanged < FirstParamChanged.LaplacianPyramid)
                    _laplacianFilterOperation.SourceChanged();
                if (_firstParamChanged < FirstParamChanged.LocalContrast)
                    _localContrastOperation.SourceChanged();
                if (_firstParamChanged < FirstParamChanged.ColorTone)
                    _colorToneOperation.SourceChanged();
                _firstParamChanged = FirstParamChanged.None;
                ApplyAstroStretchOperation();
                ApplyLaplacianFilterOperation();
                if (SourceBitmap is null || _updateTimer.IsEnabled)
                    return;
                ApplyLocalContrastOperation();
                if (SourceBitmap is null || _updateTimer.IsEnabled)
                    return;
                BrightnessContrastOperation.ApplyContrast(_localContrastOperation.DstBitmap, (float)Contrast);
                ApplyColorToneOperation();
                var srcBitmap = SourceBitmap;
                if (srcBitmap is null)
                    return;
                (PreviewPictureSource, var histogramTask) = _localContrastOperation.DstBitmap.ToBitmapSourceWithHistogram(srcBitmap.DpiX, srcBitmap.DpiY, FloatBitmap.DefaultMonitorGamma);
                await histogramTask.ContinueWith(task => SetHistogram(task.Result), TaskScheduler.Current);
            }));
            Mouse.OverrideCursor = null;
        }

        public void ShowSourceHistogram()
        {
            Task.Run(() =>
            {
                if (SourceBitmap is null)
                    return;
                (_, var histogramTask) = _sourceFloatBitmap.ToBitmapSourceWithHistogram(SourceBitmap.DpiX, SourceBitmap.DpiY, FloatBitmap.DefaultMonitorGamma);
                histogramTask.ContinueWith(task => SetHistogram(task.Result), TaskScheduler.Current);
            });
        }

        private void SetHistogram(int[] histogram)
        {
            var histogramPoints = new PointCollection([new Point(0, 0)]);
            for (int i = 0; i < histogram.Length; i++)
                histogramPoints.Add(new Point(i, -histogram[i]));
            histogramPoints.Add(new Point(histogram.Length - 1, 0));
            histogramPoints.Freeze();
            HistogramPoints = histogramPoints;
        }

        public bool IsNoOperation => (!IsAstroModeEnabled || AstroStretch == 0 && BackgroundRemovalSmooth == 0 && BlackPoint == 0)
            && DetailHandling == 1 && ToneMapping == 1 && HighlightStrength == 0 && ShadowStrength == 0 && Contrast == DefaultContrast
            && !_colorToneOperation.AreToneAdjustmentsChanged;

        public BitmapSource GetResultImage(bool produce16bitOutput)
        {
            if (produce16bitOutput)
            {
                var srcBitmap = SourceBitmap ?? throw new InvalidOperationException("Source bitmap not set");
                return _localContrastOperation.DstBitmap.ToBitmapSource16(srcBitmap.DpiX, srcBitmap.DpiY, FloatBitmap.DefaultMonitorGamma);
            }
            return PreviewPictureSource ?? throw new InvalidOperationException("Operation has not completed");
        }

        public BitmapSource ApplyOperations(BitmapSource source)
        {
            if (IsNoOperation) 
                return source;
            _sourceFloatBitmap.Assign(source, FloatBitmap.DefaultMonitorGamma);
            ApplyAstroStretchOperation();
            _laplacianFilterOperation.SourceChanged();
            ApplyLaplacianFilterOperation();
            _localContrastOperation.SourceChanged();
            ApplyLocalContrastOperation();
            BrightnessContrastOperation.ApplyContrast(_localContrastOperation.DstBitmap, (float)Contrast);
            _colorToneOperation.SourceChanged();
            ApplyColorToneOperation();
            return _localContrastOperation.DstBitmap.ToBitmapSource(source.DpiX, source.DpiY, FloatBitmap.DefaultMonitorGamma);
        }

        public async Task FinishPreviewAsync()
        {
            if (_updateTimer.IsEnabled)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await UpdatePreviewAsync();
            }
            await _previewTask; // We might have multiple instance of UpdatePreviewAsync running at the same time
            await _previewTask;
        }

        public PointCollection? HistogramPoints
        {
            get;
            private set => SetProperty(ref field, value);            
        }
    }
}
