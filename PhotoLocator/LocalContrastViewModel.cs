using MeeSoft.ImageProcessing.Operations;
using PhotoLocator.BitmapOperations;
using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        static readonly List<double> _adjustmentClipboard = [];
        static readonly List<double> _lastUsedValues = [];
        readonly DispatcherTimer _updateTimer;
        readonly LaplacianFilterOperation _laplacianFilterOperation = new() { SrcBitmap = new() };
        readonly IncreaseLocalContrastOperation _localContrastOperation = new() { DstBitmap = new() };
        readonly ColorToneAdjustOperation _colorToneOperation = new();
        Task _previewTask = Task.CompletedTask;
        bool _laplacianPyramidParamsChanged, _localContrastParamsChanged;

        public LocalContrastViewModel()
        {
            _updateTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background,
                async (s, e) => await UpdatePreviewAsync(),
                Application.Current.Dispatcher) { IsEnabled = false };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            StartUpdateTimer(false, false);
        }

        bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, newValue))
                return false;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        public BitmapSource? SourceBitmap
        {
            get => _sourceBitmap;
            set
            {
                _sourceBitmap = value;
                _previewPictureSource = value;
                _laplacianFilterOperation.SourceChanged();
                _localContrastOperation.SourceChanged();
                _colorToneOperation.SourceChanged();
                if (value is not null)
                {
                    Mouse.OverrideCursor = Cursors.AppStarting;
                    _laplacianFilterOperation.SrcBitmap.Assign(value, FloatBitmap.DefaultMonitorGamma);
                    _updateTimer.Start();
                }
            }
        }
        private BitmapSource? _sourceBitmap;

        public BitmapSource? PreviewPictureSource
        {
            get => _previewPictureSource;
            set => SetProperty(ref _previewPictureSource, value);
        }
        private BitmapSource? _previewPictureSource;

        public int PreviewZoom
        {
            get => _previewZoom;
            set => SetProperty(ref _previewZoom, value);
        }
        private int _previewZoom;

        public ICommand ToggleZoomCommand => new RelayCommand(o => PreviewZoom = PreviewZoom > 0 ? 0 : 1);
        public ICommand ZoomToFitCommand => new RelayCommand(o => PreviewZoom = 0);
        public ICommand Zoom100Command => new RelayCommand(o => PreviewZoom = 1);
        public ICommand Zoom200Command => new RelayCommand(o => PreviewZoom = 2);
        public ICommand Zoom400Command => new RelayCommand(o => PreviewZoom = 4);
        public ICommand ZoomInCommand => new RelayCommand(o => PreviewZoom = Math.Min(PreviewZoom + 1, 4));
        public ICommand ZoomOutCommand => new RelayCommand(o => PreviewZoom = Math.Max(PreviewZoom - 1, 0));

        public double HighlightStrength
        {
            get => _highlightStrength;
            set
            {
                if (SetProperty(ref _highlightStrength, RealMath.EnsureRange(value, 0, 100)))
                    StartUpdateTimer(false, true);
            }
        }
        private double _highlightStrength = DefaultHighlightStrength;

        public const double DefaultHighlightStrength = 10;

        public ICommand ResetHighlightCommand => new RelayCommand(o => HighlightStrength = DefaultHighlightStrength);

        public double ShadowStrength
        {
            get => _shadowStrength;
            set
            {
                if (SetProperty(ref _shadowStrength, RealMath.EnsureRange(value, 0, 100)))
                    StartUpdateTimer(false, true);
            }
        }
        private double _shadowStrength = DefaultShadowStrength;

        public const double DefaultShadowStrength = 10;

        public ICommand ResetShadowCommand => new RelayCommand(o => ShadowStrength = DefaultShadowStrength);

        public double MaxStretch
        {
            get => _maxStretch;
            set
            {
                if (SetProperty(ref _maxStretch, RealMath.EnsureRange(value, 0, 100)))
                    StartUpdateTimer(false, true);
            }
        }
        private double _maxStretch = DefaultMaxStretch;

        public const double DefaultMaxStretch = 50;

        public ICommand ResetMaxStretchCommand => new RelayCommand(o => MaxStretch = DefaultMaxStretch);

        public double OutlierReductionStrength
        {
            get => _outlierReductionStrength;
            set
            {
                if (SetProperty(ref _outlierReductionStrength, value))
                    StartUpdateTimer(false, true);
            }
        }
        private double _outlierReductionStrength = DefaultOutlierReductionStrength;

        public const double DefaultOutlierReductionStrength = 10;

        public ICommand ResetOutlierReductionCommand => new RelayCommand(o => OutlierReductionStrength = DefaultOutlierReductionStrength);

        public double Contrast
        {
            get => _contrast;
            set
            {
                if (SetProperty(ref _contrast, value))
                    StartUpdateTimer(false, true);
            }
        }
        private double _contrast = DefaultContrast;

        public const double DefaultContrast = 1;

        public ICommand ResetContrastCommand => new RelayCommand(o => Contrast = DefaultContrast);

        public double ToneMapping
        {
            get => _toneMapping;
            set
            {
                if (SetProperty(ref _toneMapping, value))
                    StartUpdateTimer(true, true);
            }
        }
        private double _toneMapping = DefaultToneMapping;

        public const double DefaultToneMapping = 1;

        public ICommand ResetToneMappingCommand => new RelayCommand(o => ToneMapping = DefaultToneMapping);

        public double DetailHandling
        {
            get => _detailHandling;
            set
            {
                if (SetProperty(ref _detailHandling, value))
                    StartUpdateTimer(true, true);
            }
        }
        private double _detailHandling = DefaultDetailHandling;

        public const double DefaultDetailHandling = 1;

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
            get => _activeToneIndex;
            set
            {
                if (SetProperty(ref _activeToneIndex, value) && value >= 0)
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
        }
        private int _activeToneIndex = ColorToneAdjustOperation.NumberOfTones;

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
                    StartUpdateTimer(false, false);
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
                    StartUpdateTimer(false, false);
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
                    StartUpdateTimer(false, false);
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
                    StartUpdateTimer(false, false);
            }
        }

        public ICommand ResetHueUniformityCommand => new RelayCommand(o => HueUniformity = 0);

        public double ToneRotation
        {
            get => _toneRotation;
            set
            {
                if (SetProperty(ref _toneRotation, RealMath.EnsureRange(value, -0.5, 0.5)))
                {
                    UpdateColorTones();
                    StartUpdateTimer(false, false);
                }
            }
        }
        double _toneRotation;

        public ICommand ResetToneRotationCommand => new RelayCommand(o => ToneRotation = 0);

        public ICommand ShowOriginalCommand => new RelayCommand(o =>
        {
            PreviewPictureSource = SourceBitmap;
        });

        public ICommand ResetCommand => new RelayCommand(o =>
        {
            HighlightStrength = DefaultHighlightStrength;
            ShadowStrength = DefaultShadowStrength;
            MaxStretch = DefaultMaxStretch;
            OutlierReductionStrength = DefaultOutlierReductionStrength;
            Contrast = DefaultContrast;
            ToneMapping = DefaultToneMapping;
            DetailHandling = DefaultDetailHandling;
            _colorToneOperation.ResetToneAdjustments();
            ToneRotation = 0;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = ColorToneAdjustOperation.NumberOfTones;
            StartUpdateTimer(false, false);
        });

        public ICommand CopyAdjustmentsCommand => new RelayCommand(o => StoreAdjustmentValues(_adjustmentClipboard));

        public ICommand PasteAdjustmentsCommand => new RelayCommand(o => RestoreAdjustmentValues(_adjustmentClipboard), o => _adjustmentClipboard.Count > 0);

        public ICommand RestoreLastUsedValuesCommand => new RelayCommand(o => RestoreAdjustmentValues(_lastUsedValues), o => _lastUsedValues.Count > 0);

        public void SaveLastUsedValues()
        {
            StoreAdjustmentValues(_lastUsedValues);
        }

        private void StoreAdjustmentValues(List<double> valueStore)
        {
            valueStore.Clear();
            valueStore.Add(HighlightStrength);
            valueStore.Add(ShadowStrength);
            valueStore.Add(MaxStretch);
            valueStore.Add(OutlierReductionStrength);
            valueStore.Add(ToneMapping);
            valueStore.Add(DetailHandling);
            valueStore.Add(ToneRotation);
            for (int i = 0; i < _colorToneOperation.ToneAdjustments.Length; i++)
            {
                valueStore.Add(_colorToneOperation.ToneAdjustments[i].AdjustHue);
                valueStore.Add(_colorToneOperation.ToneAdjustments[i].AdjustSaturation);
                valueStore.Add(_colorToneOperation.ToneAdjustments[i].AdjustIntensity);
                valueStore.Add(_colorToneOperation.ToneAdjustments[i].HueUniformity);
            }
        }

        private void RestoreAdjustmentValues(List<double> valueStore)
        {
            int a = 0;
            HighlightStrength = valueStore[a++];
            ShadowStrength = valueStore[a++];
            MaxStretch = valueStore[a++];
            OutlierReductionStrength = valueStore[a++];
            ToneMapping = valueStore[a++];
            DetailHandling = valueStore[a++];
            ToneRotation = valueStore[a++];
            for (int i = 0; i < _colorToneOperation.ToneAdjustments.Length; i++)
            {
                _colorToneOperation.ToneAdjustments[i].AdjustHue = (float)valueStore[a++];
                _colorToneOperation.ToneAdjustments[i].AdjustSaturation = (float)valueStore[a++];
                _colorToneOperation.ToneAdjustments[i].AdjustIntensity = (float)valueStore[a++];
                _colorToneOperation.ToneAdjustments[i].HueUniformity = (float)valueStore[a++];
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = 0;
            StartUpdateTimer(false, false);
            if (a != valueStore.Count)
                throw new InvalidOperationException("Unexpected number of adjustments");
        }

        private void StartUpdateTimer(bool laplacianPyramidParamsChanged, bool localContrastParamsChanged)
        {
            Mouse.OverrideCursor = Cursors.AppStarting;
            _laplacianPyramidParamsChanged |= laplacianPyramidParamsChanged;
            _localContrastParamsChanged |= localContrastParamsChanged;
            _updateTimer.Stop();
            _updateTimer.Start();
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
            await (_previewTask = Task.Run(() =>
            {
                if (_laplacianPyramidParamsChanged || _localContrastParamsChanged)
                {
                    _localContrastParamsChanged = false;
                    _colorToneOperation.SourceChanged();
                }
                if (_laplacianPyramidParamsChanged)
                {
                    _localContrastOperation.SourceChanged();
                    _laplacianPyramidParamsChanged = false;
                }
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
                PreviewPictureSource = _localContrastOperation.DstBitmap.ToBitmapSource(srcBitmap.DpiX, srcBitmap.DpiY, FloatBitmap.DefaultMonitorGamma);
            }));
            Mouse.OverrideCursor = null;
        }

        public bool IsNoOperation => DetailHandling == 1 && ToneMapping == 1 && HighlightStrength == 0 && ShadowStrength == 0 && Contrast == DefaultContrast 
            && !_colorToneOperation.AreToneAdjustmentsChanged;

        public BitmapSource ApplyOperations(BitmapSource source)
        {
            if (IsNoOperation) 
                return source;
            _laplacianFilterOperation.SrcBitmap.Assign(source, FloatBitmap.DefaultMonitorGamma);
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
    }
}
