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
    class LocalContrastViewModel : INotifyPropertyChanged
    {
        static readonly List<double> _adjustmentClipboard = [];
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

        public const double DefaultHighlightStrength = 10;

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

        public const double DefaultShadowStrength = 10;

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

        public const double DefaultMaxStretch = 50;

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

        public const double DefaultOutlierReductionStrength = 10;

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

        public const double DefaultToneMapping = 1;

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

        public const double DefaultDetailHandling = 1;

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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HueAdjust)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaturationAdjust)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntensityAdjust)));
                }
            }
        }
        private int _activeToneIndex;

        public float HueAdjust
        {
            get => _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustHue;
            set
            {
                if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustHue, value))
                    StartUpdateTimer(false, false);
            }
        }

        public float SaturationAdjust
        {
            get => _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustSaturation;
            set
            {
                if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustSaturation, value))
                    StartUpdateTimer(false, false);
            }
        }

        public float IntensityAdjust
        {
            get => _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustIntensity;
            set
            {
                if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].AdjustIntensity, value))
                    StartUpdateTimer(false, false);
            }
        }

        public float HueUniformity
        {
            get => _colorToneOperation.ToneAdjustments[ActiveToneIndex].HueUniformity;
            set
            {
                if (SetProperty(ref _colorToneOperation.ToneAdjustments[ActiveToneIndex].HueUniformity, value))
                    StartUpdateTimer(false, false);
            }
        }

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
            ToneMapping = DefaultToneMapping;
            DetailHandling = DefaultDetailHandling;
            _colorToneOperation.ResetToneAdjustments();
            ToneRotation = 0;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = 0;
            StartUpdateTimer(false, false);
        });

        public ICommand CopyAdjustmentsCommand => new RelayCommand(o =>
        {
            _adjustmentClipboard.Clear();
            _adjustmentClipboard.Add(HighlightStrength);
            _adjustmentClipboard.Add(ShadowStrength);
            _adjustmentClipboard.Add(MaxStretch);
            _adjustmentClipboard.Add(OutlierReductionStrength);
            _adjustmentClipboard.Add(ToneMapping);
            _adjustmentClipboard.Add(DetailHandling);
            _adjustmentClipboard.Add(ToneRotation);
            for (int i = 0; i < _colorToneOperation.ToneAdjustments.Length; i++)
            {
                _adjustmentClipboard.Add(_colorToneOperation.ToneAdjustments[i].AdjustHue);
                _adjustmentClipboard.Add(_colorToneOperation.ToneAdjustments[i].AdjustSaturation);
                _adjustmentClipboard.Add(_colorToneOperation.ToneAdjustments[i].AdjustIntensity);
                _adjustmentClipboard.Add(_colorToneOperation.ToneAdjustments[i].HueUniformity);
            }
        });

        public ICommand PasteAdjustmentsCommand => new RelayCommand(o =>
        {
            if (_adjustmentClipboard.Count == 0)
                return;
            int a = 0;
            HighlightStrength = _adjustmentClipboard[a++];
            ShadowStrength = _adjustmentClipboard[a++];
            MaxStretch = _adjustmentClipboard[a++];
            OutlierReductionStrength = _adjustmentClipboard[a++];
            ToneMapping = _adjustmentClipboard[a++];
            DetailHandling = _adjustmentClipboard[a++];
            ToneRotation = _adjustmentClipboard[a++];
            for (int i = 0; i < _colorToneOperation.ToneAdjustments.Length; i++)
            {
                _colorToneOperation.ToneAdjustments[i].AdjustHue = (float)_adjustmentClipboard[a++];
                _colorToneOperation.ToneAdjustments[i].AdjustSaturation = (float)_adjustmentClipboard[a++];
                _colorToneOperation.ToneAdjustments[i].AdjustIntensity = (float)_adjustmentClipboard[a++];
                _colorToneOperation.ToneAdjustments[i].HueUniformity = (float)_adjustmentClipboard[a++];
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            ActiveToneIndex = 0;
            StartUpdateTimer(false, false);
            if (a != _adjustmentClipboard.Count)
                throw new InvalidOperationException("Unexpected number of adjustments");
        });

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
                ApplyColorToneOperation();
                var srcBitmap = SourceBitmap;
                if (srcBitmap is null)
                    return;
                PreviewPictureSource = _localContrastOperation.DstBitmap.ToBitmapSource(srcBitmap.DpiX, srcBitmap.DpiY, FloatBitmap.DefaultMonitorGamma);
            }));
            Mouse.OverrideCursor = null;
        }

        public BitmapSource ApplyOperations(BitmapSource source)
        {
            _laplacianFilterOperation.SrcBitmap.Assign(source, FloatBitmap.DefaultMonitorGamma);
            _laplacianFilterOperation.SourceChanged();
            ApplyLaplacianFilterOperation();
            _localContrastOperation.SourceChanged();
            ApplyLocalContrastOperation();
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
