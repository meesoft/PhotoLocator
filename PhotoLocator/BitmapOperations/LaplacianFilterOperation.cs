#nullable disable

using PhotoLocator.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    /// <summary>
    /// Laplacian pyramid filter.
    /// Inspired by the paper "Local Laplacian Filters: Edge-aware Image Processing with a Laplacian Pyramid"
    /// by Sylvain Paris, Samuel W. Hasinoff and Jan Kautz
    /// </summary>
    public class LaplacianFilterOperation : OperationBase
    {
        public enum OperationType
        {
            RemapSource,
            RemapSourceInterpolate,
            RemapSourceInterpolate16,
            RemapSourceFull,
            RemapSourceFull16,
        }

        public OperationType Operation
        {
            get { return _remapLaplacian; }
            set
            {
                if (_remapLaplacian != value)
                {
                    _remapLaplacian = value;
                    _toneMappedPlane = null;
                }
            }
        }
        OperationType _remapLaplacian;

        /// <summary>
        /// Process in log domain
        /// </summary>
        public bool LogDomain
        {
            get { return _logDomain; }
            set
            {
                if (_logDomain != value)
                {
                    _logDomain = value;
                    _remapLUTValid = false;
                    _toneMappedPlane = null;
                }
            }
        }
        bool _logDomain = true;

        /// <summary>
        /// Edge or detail selection threshold (sigma_r)
        /// </summary>
        public float Threshold
        {
            get { return _threshold; }
            set
            {
                if (_threshold != value)
                {
                    _threshold = value;
                    _remapLUTValid = false;
                    _toneMappedPlane = null;
                }
            }
        }
        float _threshold;

        /// <summary>
        /// Detail parameter.
        /// Choose Alpha ]0;1[ for detail enhacement.
        /// Choose Alpha>1 for detail smoothing.
        /// </summary>
        public float Alpha
        {
            get { return _alpha; }
            set
            {
                if (_alpha != value)
                {
                    _alpha = value;
                    _toneMappedPlane = null;
                    _remapLUTValid = false;
                }
            }
        }
        float _alpha = 1;

        /// <summary>
        /// Tone mapping parameter.
        /// Choose Beta [0;1[ for tone mapping compression.
        /// Choose Beta>1 for inverse tone mapping.
        /// </summary>
        public float Beta
        {
            get { return _beta; }
            set
            {
                if (_beta != value)
                {
                    _beta = value;
                    _remapLUTValid = false;
                    _toneMappedPlane = null;
                }
            }
        }
        float _beta = 0.5f;

        /// <summary>
        /// Noise level for detail enhancement (Alpha below 1)
        /// </summary>
        public float NoiseLevel
        {
            get { return _noiseLevel; }
            set
            {
                if (_noiseLevel != value)
                {
                    _noiseLevel = value;
                    _remapLUTValid = false;
                    _toneMappedPlane = null;
                }
            }
        }
        float _noiseLevel = 0.01f;

        /// <summary>
        /// The interval [MinLevel;MaxLevel] determines which levels of the Laplacian pyramid are processed
        /// </summary>
        public int MinLevel
        {
            get { return _minLevel; }
            set
            {
                if (_minLevel != value)
                {
                    _minLevel = value;
                    _toneMappedPlane = null;
                }
            }
        }
        int _minLevel;

        /// <summary>
        /// The interval [MinLevel;MaxLevel] determines which levels of the Laplacian pyramid are processed
        /// </summary>
        public int MaxLevel
        {
            get { return _maxLevel; }
            set
            {
                if (_maxLevel != value)
                {
                    _maxLevel = value;
                    _toneMappedPlane = null;
                }
            }
        }
        int _maxLevel = int.MaxValue;

        /// <summary>
        /// Outlier clipping filter (NaN to disable final histogram stretch)
        /// </summary>
        public float OutlierReductionFilterSize { get; set; } = float.NaN;

        /// <summary>
        /// For progressive display of the operation
        /// </summary>
        public Action<float> ProgressCallback { get; set; }

        /// <summary>
        /// Error message in case external C++ implementation fails
        /// </summary>
        public string ExternalOpError
        {
            get; private set;
        }

        /// <summary>
        /// Call SourceChanged() when source bitmap is changed to reset any internal structures
        /// </summary>
        public override void SourceChanged()
        {
            base.SourceChanged();
            _remapLUTValid = false;
            _luminancePlane = null;
            _normalizedColorPlanes = null;
            _toneMappedPlane = null;
        }

        FloatBitmap _luminancePlane, _toneMappedPlane;
        FloatBitmap _normalizedColorPlanes;

        public override void Apply()
        {
            ExternalOpError = null;
            if (Alpha <= 0)
                throw new ArgumentException("Alpha");
            var bitmap = SrcBitmap;
            if (bitmap.PlaneCount == 1)
                ApplyToPlane(SrcBitmap, DstBitmap);
            else
            {
                // Compute luminance plane
                if (_luminancePlane == null)
                {
                    _luminancePlane = ConvertToGrayscaleOperation.ConvertToGrayscale(bitmap, false);
                    _luminancePlane.Add(-_luminancePlane.Min() + 1e-6f); // Add a small number so that we can invert it for color normalization
                }

                // Normalize color planes
                if (_normalizedColorPlanes == null)
                {
                    _normalizedColorPlanes = new FloatBitmap(bitmap);
                    _normalizedColorPlanes.ProcessElementWise(_luminancePlane, (c, l) => c / l);
                }

                // Tone map luminance plane
                var mappedPlane = new FloatBitmap();
                ApplyToPlane(_luminancePlane, mappedPlane);

                // Restore colors
                DstBitmap.Assign(_normalizedColorPlanes);
                DstBitmap.ProcessElementWise(mappedPlane, (c, l) => c * l);
            }
        }

        void ApplyToPlane(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            if (_toneMappedPlane == null)
            {
                // Compute tone mapped luminance plane
                var toneMappedPlane = new FloatBitmap(srcPlane);
                if (LogDomain)
                {
                    _sigmaR = Math.Max(1e-5f, (float)Math.Exp(Threshold));
                    const double eps = 1e-8;
                    toneMappedPlane.ProcessElementWise(a => (float)Math.Log(a + eps));
                    ApplyOperation(toneMappedPlane, toneMappedPlane);
                    toneMappedPlane.ProcessElementWise(a => (float)(Math.Exp(a) - eps));
                }
                else
                {
                    _sigmaR = Math.Max(1e-5f, Threshold);
                    ApplyOperation(srcPlane, toneMappedPlane);
                }

                _toneMappedPlane = toneMappedPlane;
            }

            dstPlane.Assign(_toneMappedPlane);
            if (!float.IsNaN(OutlierReductionFilterSize))
            {
                // Stretch histogram to [0;1] using smooth filter to avoid outliers
                var smoothPlane = dstPlane;
                if (OutlierReductionFilterSize > 0)
                {
                    smoothPlane = new FloatBitmap(smoothPlane);
                    IIRSmoothOperation.Apply(smoothPlane, OutlierReductionFilterSize);
                }
                var (min, max) = smoothPlane.MinMax();
                var scale = 1 / (max - min);
                dstPlane.ProcessElementWise(p => (p - min) * scale);
            }
        }

        private void ApplyOperation(FloatBitmap srcPlane, FloatBitmap toneMappedPlane)
        {
            if (Operation == OperationType.RemapSource)
            {
                if (Alpha == 1f)
                    LapFilterCoreInterpolate(srcPlane, toneMappedPlane);
                else
                    LapFilterCoreFull16(srcPlane, toneMappedPlane);
            }
            else if (Operation == OperationType.RemapSourceFull)
                LapFilterCoreFull(srcPlane, toneMappedPlane);
            else if (Operation == OperationType.RemapSourceFull16)
                LapFilterCoreFull16(srcPlane, toneMappedPlane);
            else if (Operation == OperationType.RemapSourceInterpolate)
                LapFilterCoreInterpolate(srcPlane, toneMappedPlane);
            else if (Operation == OperationType.RemapSourceInterpolate16)
                LapFilterCoreInterpolate16(srcPlane, toneMappedPlane);
            else
                throw new NotImplementedException(Operation + " not implemented");
        }

        float _sigmaR;

        void LapFilterCoreFull(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            int levels = GetMaxLevels(srcPlane);

            if (!_remapLUTValid)
            {
                var (min, max) = srcPlane.MinMax();
                CreateRemapLUT(min, max);
            }

            // Construct pyramid of source image
            var pyrG = new FloatBitmap[levels];
            var pyrL = new FloatBitmap[levels];
            BuildLaplacianPyramid(srcPlane, pyrG, pyrL);
            // Process selected levels
            for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
            {
                // Update pyramid level
                Parallel.For(0, pyrL[level].Size, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    () =>
                    {
                        var remapPyrG = new FloatBitmap[level + 2]; // We need an extra level because the last is the residual low pass image
                        for (int i = 0; i < remapPyrG.Length; i++)
                        {
                            remapPyrG[i] = new FloatBitmap(pyrG[i].Width, pyrG[i].Height, 1);
#if DEBUG
                            remapPyrG[i].ProcessElementWise(p => float.NaN);
#endif
                        }
                        return remapPyrG;
                    },
                    (i, state, remapPyrG) =>
                    {
                        int x = i % pyrL[level].Width;
                        int y = i / pyrL[level].Width;
                        var roi = new ROI(x, y);
                        var nextLevelROI = BilinearResizeOperation.GetSourceROI(remapPyrG[level + 1], remapPyrG[level], in roi);
                        GaussianPyramidRemap(srcPlane, remapPyrG, level + 1, nextLevelROI, level, in roi, pyrG[level][x, y]);
                        pyrL[level][x, y] = LaplacianPyramidCoefficient(remapPyrG, level, in roi);
                        Debug.Assert(!float.IsNaN(pyrL[level][x, y]));
                        return remapPyrG;
                    },
                    remapPyrG =>
                    {
                    });
                // Progress display callback
                ProgressCallback?.Invoke(95f * RealMath.Sqr(levels - 1 - level) / RealMath.Sqr(levels - 1));
            }
            // Reconstruct image from pyramid
            Debug.Assert(pyrG[levels - 1] == pyrL[levels - 1]);
            pyrG[levels - 1] = null;
            ReconstructLaplacianPyramid(pyrL, dstPlane);
        }

        void LapFilterCoreFull16(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            int levels = GetMaxLevels(srcPlane);

            var (srcMin, srcMax) = srcPlane.MinMax();
            var plane16 = new BitmapPlaneInt16(srcPlane, p => (short)((p - srcMin) * IntRange / (srcMax - srcMin) + 0.5f));

            CreateRemapLUT16(srcMin, srcMax);

            // Construct pyramid of source image
            var pyrG = new BitmapPlaneInt16[levels];
            var pyrL = new BitmapPlaneInt16[levels];
            BuildLaplacianPyramid(plane16, pyrG, pyrL);
            // Process selected levels
            for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
            {
                // Update pyramid level
                Parallel.For(0, pyrL[level].Size, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    () =>
                    {
                        var remapPyrG = new BitmapPlaneInt16[level + 2]; // We need an extra level because the last is the residual low pass image
                        for (int i = 0; i < remapPyrG.Length; i++)
                            remapPyrG[i] = new BitmapPlaneInt16(pyrG[i].Width, pyrG[i].Height);
                        return remapPyrG;
                    },
                    (i, state, remapPyrG) =>
                    {
                        int x = i % pyrL[level].Width;
                        int y = i / pyrL[level].Width;
                        var roi = new ROI(x, y);
                        var nextLevelROI = BilinearResizeOperation.GetSourceROI(remapPyrG[level + 1], remapPyrG[level], in roi);
                        GaussianPyramidRemap(plane16, remapPyrG, level + 1, nextLevelROI, level, in roi, pyrG[level][x, y]);
                        pyrL[level][x, y] = LaplacianPyramidCoefficient(remapPyrG, level, in roi);
                        return remapPyrG;
                    },
                    remapPyrG =>
                    {
                    });
                // Progress display callback
                ProgressCallback?.Invoke(95f * RealMath.Sqr(levels - 1 - level) / RealMath.Sqr(levels - 1));
            }
            _remapLUT16 = null;
            // Reconstruct image from pyramid
            var tempPlanes = pyrG;
            ReconstructLaplacianPyramid(pyrL, plane16, tempPlanes);
            dstPlane.Assign(plane16, p => p * (srcMax - srcMin) / IntRange + srcMin);
        }

        void LapFilterCoreInterpolate(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            int levels = GetMaxLevels(srcPlane);
            var (iMin, iMax) = srcPlane.MinMax();
            if (!_remapLUTValid)
                CreateRemapLUT(iMin, iMax);
            // Construct pyramid of source image
            var pyrG = new FloatBitmap[levels];
            var pyrL = new FloatBitmap[levels];
            BuildLaplacianPyramid(srcPlane, pyrG, pyrL);
#if DEBUG
            for (int level = MinLevel; level <= Math.Min(levels - 2, MaxLevel); level++)
                pyrL[level].ProcessElementWise(p => float.NaN);
#endif
            int steps = Alpha == 1 ? 16 : 256;
            float prevStepg0 = float.NaN;
            var stepPyrG = new FloatBitmap[levels];
            var stepPyrL = new FloatBitmap[levels];
            var prevStepPyrL = new FloatBitmap[levels];
            var remappedSource = new FloatBitmap(srcPlane.Width, srcPlane.Height, 1);
            for (int j = 0; j <= steps; j++)
            {
                float stepg0;
                if (j == 0)
                    stepg0 = iMin;
                else if (j == steps)
                    stepg0 = iMax;
                else
                    stepg0 = iMin + (iMax - iMin) * j / steps;
                RemapPlane(srcPlane, remappedSource, stepg0);
                BuildGaussianPyramid(remappedSource, stepPyrG);
                if (j == 0)
                {
                    // Prepare "previous" step values for first step
                    for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
                        BuildLaplacianPyramidLevel(stepPyrG, stepPyrL, level);
                }
                else
                {
                    // Process selected levels
                    for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
                    {
                        BuildLaplacianPyramidLevel(stepPyrG, stepPyrL, level);
                        Parallel.For(0, pyrL[level].Height, y =>
                        {
                            var width = pyrL[level].Width;
                            unsafe
                            {
                                fixed (float* pyrGLine = &pyrG[level].Elements[y, 0])
                                fixed (float* prevStepPyrLLine = &prevStepPyrL[level].Elements[y, 0])
                                fixed (float* stepPyrLLine = &stepPyrL[level].Elements[y, 0])
                                fixed (float* pyrLLine = &pyrL[level].Elements[y, 0])
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        var g0 = pyrGLine[x];
                                        if ((g0 >= prevStepg0 || j == 1) && (g0 <= stepg0 || j == steps))
                                        {
                                            var a = (g0 - prevStepg0) / (stepg0 - prevStepg0);
                                            pyrLLine[x] = prevStepPyrLLine[x] * (1 - a) + stepPyrLLine[x] * a;
                                        }
                                        Debug.Assert(j < steps || !float.IsNaN(pyrLLine[x]));
                                    }
                                }
                            }
                        });
                    }
                }
                prevStepg0 = stepg0;
                (prevStepPyrL, stepPyrL) = (stepPyrL, prevStepPyrL);
                // Progress display callback
                ProgressCallback?.Invoke(2 + j * 95 / steps);
            }
            // Reconstruct image from pyramid
            Debug.Assert(pyrG[levels - 1] == pyrL[levels - 1]);
            pyrG[levels - 1] = null;
            ReconstructLaplacianPyramid(pyrL, dstPlane);
        }

        void LapFilterCoreInterpolate16(FloatBitmap srcPlane, FloatBitmap dstPlane)
        {
            int levels = GetMaxLevels(srcPlane);

            var (srcMin,srcMax) = srcPlane.MinMax();
            var plane16 = new BitmapPlaneInt16(srcPlane, p => (short)((p - srcMin) * IntRange / (srcMax - srcMin) + 0.5f));

            CreateRemapLUT16(srcMin, srcMax);

            // Construct pyramid of source image
            var pyrG = new BitmapPlaneInt16[levels];
            var pyrL = new BitmapPlaneInt16[levels];
            BuildLaplacianPyramid(plane16, pyrG, pyrL);

            int steps = Alpha == 1 ? 16 : 256;
            int prevStepg0 = 0;
            var stepPyrG = new BitmapPlaneInt16[levels];
            var stepPyrL = new BitmapPlaneInt16[levels];
            var prevStepPyrL = new BitmapPlaneInt16[levels];
            var remappedSource = new BitmapPlaneInt16(plane16.Width, plane16.Height);
            for (int j = 0; j <= steps; j++)
            {
                int stepg0;
                if (j == 0)
                    stepg0 = 0;
                else if (j == steps)
                    stepg0 = IntRange;
                else
                    stepg0 = IntRange * j / steps;
                RemapPlane(plane16, remappedSource, stepg0);
                BuildGaussianPyramid(remappedSource, stepPyrG);
                if (j == 0)
                {
                    for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
                        BuildLaplacianPyramidLevel(stepPyrG, stepPyrL, level);
                }
                else
                {
                    // Process selected levels
                    for (int level = Math.Min(levels - 2, MaxLevel); level >= MinLevel; level--)
                    {
                        BuildLaplacianPyramidLevel(stepPyrG, stepPyrL, level);
                        Parallel.For(0, pyrL[level].Height, y =>
                        {
                            var width = pyrL[level].Width;
                            unsafe
                            {
                                fixed (Int16* pyrGLine = &pyrG[level].Elements[y, 0])
                                fixed (Int16* prevStepPyrLLine = &prevStepPyrL[level].Elements[y, 0])
                                fixed (Int16* stepPyrLLine = &stepPyrL[level].Elements[y, 0])
                                fixed (Int16* pyrLLine = &pyrL[level].Elements[y, 0])
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        var g0 = pyrGLine[x];
                                        if (g0 >= prevStepg0 && g0 <= stepg0)
                                        {
                                            var a = (g0 - prevStepg0) * 65536 / (stepg0 - prevStepg0);
                                            pyrLLine[x] = (Int16)((prevStepPyrLLine[x] * (65536 - a) + stepPyrLLine[x] * a + 32768) >> 16);
                                        }
                                    }
                                }
                            }
                        });
                    }
                }
                prevStepg0 = stepg0;
                (prevStepPyrL, stepPyrL) = (stepPyrL, prevStepPyrL);
                // Progress display callback
                ProgressCallback?.Invoke(2 + j * 95 / steps);
            }
            _remapLUT16 = null;
            // Reconstruct image from pyramid
            ReconstructLaplacianPyramid(pyrL, plane16);
            dstPlane.Assign(plane16, p => p * (srcMax - srcMin) / IntRange + srcMin);
        }

        /// <summary>
        /// Remap plane based on g0 and precomputed LUT
        /// </summary>
        void RemapPlane(FloatBitmap srcPlane, FloatBitmap dstPlane, float g0)
        {
            dstPlane.New(srcPlane.Width, srcPlane.Height, 1);
            Parallel.For(0, srcPlane.Height, y =>
            {
                float sub = g0 + _minLUT;
                unsafe
                {
                    fixed (float* srcLine = &srcPlane.Elements[y, 0])
                    fixed (float* dstLine = &dstPlane.Elements[y, 0])
                    fixed (float* remapLUT = _remapLUT)
                    {
                        for (int x = 0; x < srcPlane.Width; x++)
                            //dstLine[x] = RemapLUT(srcLine[x] - g0) + g0;
                            dstLine[x] = remapLUT[(int)(0.5f + (srcLine[x] - sub) * _scaleLUT)] + g0;
                    }
                }
            });
        }
        void RemapPlane(BitmapPlaneInt16 srcPlane, BitmapPlaneInt16 dstPlane, int g0)
        {
            dstPlane.New(srcPlane.Width, srcPlane.Height);
            Parallel.For(0, srcPlane.Height, y =>
            {
                unsafe
                {
                    fixed (Int16* srcLine = &srcPlane.Elements[y, 0])
                    fixed (Int16* dstLine = &dstPlane.Elements[y, 0])
                    fixed (short* remapLUT = _remapLUT16)
                    {
                        var width = srcPlane.Width;
                        for (int x = 0; x < width; x++)
                        {
                            Debug.Assert(IntMath.InRange((srcLine[x] - g0) + IntRange, 0, _remapLUT16.Length - 1));
                            var res = remapLUT[(srcLine[x] - g0) + IntRange] + g0;
                            if (res < short.MinValue)
                                res = short.MinValue;
                            if (res > short.MaxValue)
                                res = short.MaxValue;
                            dstLine[x] = (short)(res);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Detail remapping function
        /// </summary>
        float Fd(float d)
        {
            float result = (float)Math.Pow(d, Alpha);
            if (Alpha < 1)
            {
                float tau = RealMath.SmoothStep(d * _sigmaR, NoiseLevel, 2 * NoiseLevel);
                result = tau * result + (1 - tau) * d;
            }
            return result;
        }

        /// <summary>
        /// Luminance remapping function
        /// </summary>
        float Remap(float i)
        {
            // Detail and edge processing, separation based on sigma_r threshold
            float dsgn = 1;
            float dnrm = i;
            if (dnrm < 0)
            {
                dsgn = -1;
                dnrm = -dnrm;
            }
            if (dnrm > _sigmaR) // Edge
                return dsgn * (Beta * (dnrm - _sigmaR) + _sigmaR);
            if (Alpha == 1) // Detail
                return i; // g0 + dsgn * dnrm;
            return dsgn * _sigmaR * Fd(dnrm / _sigmaR);
        }

        bool _remapLUTValid;
        float[] _remapLUT;
        short[] _remapLUT16;
        float _minLUT, _scaleLUT;
        const int LUTSize = 1024 * 16;
        const int IntRange = 16384;

        void CreateRemapLUT(float min, float max)
        {
            float iMin = min - max;
            float iMax = max - min;
            float iRange = iMax - iMin;
            float iScale = iRange / (LUTSize - 1);
            _remapLUT ??= new float[LUTSize];
            Parallel.For(0, LUTSize, i => _remapLUT[i] = Remap(i * iScale + iMin));
            _minLUT = iMin;
            _scaleLUT = iRange > 0 ? (LUTSize - 1) / iRange : 0;
            _remapLUTValid = true;
        }

        float RemapLUT(float i)
        {
            return _remapLUT[(int)(0.5f + (i - _minLUT) * _scaleLUT)];
        }

        void CreateRemapLUT16(float min, float max)
        {
            _remapLUT16 ??= new short[IntRange * 2 + 1];
            var scale = IntRange/ (max - min);
            Parallel.For(0, _remapLUT16.Length, i =>
            {
                var res = IntMath.Round(Remap((i - IntRange) / scale) * scale);
                if (res < short.MinValue)
                    res = short.MinValue;
                if (res > short.MaxValue)
                    res = short.MaxValue;
                _remapLUT16[i] = (short)res;
            });
        }

        /// <summary>
        /// Construct Gaussian pyramid from remapped image
        /// </summary>
        void GaussianPyramidRemap(FloatBitmap srcPlane, FloatBitmap[] pyrG, int level, ROI roi, int finalLevel, ref readonly ROI finalLevelROI, float g0)
        {
            if (level == 0)
            {
                // Apply pixel mapping to input image
                unsafe
                {
                    fixed (float* srcPixels = srcPlane.Elements)
                    fixed (float* g0Pixels = pyrG[0].Elements)
                    fixed (float* remapLUT = _remapLUT)
                    {
                        float sub = g0 + _minLUT;
                        for (int y = roi.Top; y <= roi.Bottom; y++)
                        {
                            int rowOffset = y * srcPlane.Width;
                            float* srcRow = &srcPixels[rowOffset];
                            float* g0Row = &g0Pixels[rowOffset];
                            for (int x = roi.Left; x <= roi.Right; x++)
                            {
                                //g0Row[x] = Remap(srcRow[x] - g0) + g0;
                                //g0Row[x] = RemapLUT(srcRow[x] - g0) + g0;
                                g0Row[x] = remapLUT[(int)(0.5f + (srcRow[x] - sub) * _scaleLUT)] + g0;
                                Debug.Assert(!float.IsNaN(g0Row[x]));
                            }
                        }
                    }
                }
            }
            else
            {
                if (level == finalLevel)
                    roi.Union(in finalLevelROI);

                // Recursively build pyramid
                var prevLevelROI = BilinearResizeOperation.GetSourceROI(pyrG[level - 1], pyrG[level], in roi);
                GaussianPyramidRemap(srcPlane, pyrG, level - 1, prevLevelROI, finalLevel, in finalLevelROI, g0);

                // Construct Gaussian pyramid level by downsampling
                BilinearResizeOperation.ApplyToPlane(pyrG[level - 1], pyrG[level], roi);
            }
        }

        /// <summary>
        /// Construct Gaussian pyramid from remapped image
        /// </summary>
        void GaussianPyramidRemap(BitmapPlaneInt16 srcPlane, BitmapPlaneInt16[] pyrG, int level, ROI roi, int finalLevel, ref readonly ROI finalLevelROI, short g0)
        {
            if (level == 0)
            {
                // Apply pixel mapping to input image
                unsafe
                {
                    fixed (short* srcPixels = srcPlane.Elements)
                    fixed (short* g0Pixels = pyrG[0].Elements)
                    fixed (short* remapLUT = _remapLUT16)
                    {
                        for (int y = roi.Top; y <= roi.Bottom; y++)
                        {
                            int rowOffset = y * srcPlane.Width;
                            short* srcRow = &srcPixels[rowOffset];
                            short* g0Row = &g0Pixels[rowOffset];
                            for (int x = roi.Left; x <= roi.Right; x++)
                            {
                                Debug.Assert(IntMath.InRange((srcRow[x] - g0) + IntRange, 0, _remapLUT16.Length - 1));
                                var res = remapLUT[(srcRow[x] - g0) + IntRange] + g0;
                                if (res < short.MinValue)
                                    res = short.MinValue;
                                if (res > short.MaxValue)
                                    res = short.MaxValue;
                                g0Row[x] = (short)(res);
                            }
                        }
                    }
                }
            }
            else
            {
                if (level == finalLevel)
                    roi.Union(in finalLevelROI);

                // Recursively build pyramid
                var prevLevelROI = BilinearResizeOperation.GetSourceROI(pyrG[level - 1], pyrG[level], in roi);
                GaussianPyramidRemap(srcPlane, pyrG, level - 1, prevLevelROI, finalLevel, in finalLevelROI, g0);

                // Construct Gaussian pyramid level by downsampling
                BilinearResizeOperation.ApplyToPlane(pyrG[level - 1], pyrG[level], roi);
            }
        }

        /// <summary>
        /// Determine Laplacian pyramid coefficient for single pixel ROI
        /// </summary>
        static float LaplacianPyramidCoefficient(FloatBitmap[] pyrG, int level, ref readonly ROI roi)
        {
            // Construct Laplacian pyramid level as difference between image and upsampled low pass version
            float g = pyrG[level][roi.Left, roi.Top];
            Debug.Assert(!float.IsNaN(g));
            BilinearResizeOperation.ApplyToPlane(pyrG[level + 1], pyrG[level], roi);
            return g - pyrG[level][roi.Left, roi.Top];
        }

        /// <summary>
        /// Determine Laplacian pyramid coefficient for single pixel ROI
        /// </summary>
        static short LaplacianPyramidCoefficient(BitmapPlaneInt16[] pyrG, int level, ref readonly ROI roi)
        {
            // Construct Laplacian pyramid level as difference between image and upsampled low pass version
            var g = pyrG[level][roi.Left, roi.Top];
            BilinearResizeOperation.ApplyToPlane(pyrG[level + 1], pyrG[level], roi);
            return (short)(g - pyrG[level][roi.Left, roi.Top]);
        }

        public static int GetMaxLevels(FloatBitmap I)
        {
            int levels = 0;
            int i = Math.Max(I.Width, I.Height);
            while (i > 0)
            {
                levels++;
                i /= 2;
            }
            return levels;
        }

        /// <summary>
        /// Create Gaussian pyramid
        /// </summary>
        static void BuildGaussianPyramid(FloatBitmap image, FloatBitmap[] pyrG)
        {
            // Recursively build pyramid
            pyrG[0] = image;
            for (int level = 0; level < pyrG.Length - 1; level++)
            {
                // Construct Gaussian pyramid level
                // Apply low pass filter and downsample
                if (pyrG[level + 1] == null)
                    pyrG[level + 1] = new FloatBitmap(Math.Max(1, pyrG[level].Width / 2), Math.Max(1, pyrG[level].Height / 2), 1);
                BilinearResizeOperation.ApplyToPlaneParallel(pyrG[level], pyrG[level + 1]);
            }
        }

        /// <summary>
        /// Create Gaussian pyramid
        /// </summary>
        static void BuildGaussianPyramid(BitmapPlaneInt16 image, BitmapPlaneInt16[] pyrG)
        {
            // Recursively build pyramid
            pyrG[0] = image;
            for (int level = 0; level < pyrG.Length - 1; level++)
            {
                // Construct Gaussian pyramid level
                // Apply low pass filter and downsample
                if (pyrG[level + 1] == null)
                    pyrG[level + 1] = new BitmapPlaneInt16(Math.Max(1, pyrG[level].Width / 2), Math.Max(1, pyrG[level].Height / 2));
                BilinearResizeOperation.ApplyToPlaneParallel(pyrG[level], pyrG[level + 1]);
            }
        }

        /// <summary>
        /// Create Laplacian pyramid
        /// </summary>
        static void BuildLaplacianPyramid(FloatBitmap image, FloatBitmap[] pyrG, FloatBitmap[] pyrL)
        {
            BuildGaussianPyramid(image, pyrG);
            for (int level = 0; level < pyrG.Length; level++)
                BuildLaplacianPyramidLevel(pyrG, pyrL, level);
        }

        /// <summary>
        /// Create Laplacian pyramid
        /// </summary>
        static void BuildLaplacianPyramid(BitmapPlaneInt16 image, BitmapPlaneInt16[] pyrG, BitmapPlaneInt16[] pyrL)
        {
            BuildGaussianPyramid(image, pyrG);
            for (int level = 0; level < pyrG.Length; level++)
                BuildLaplacianPyramidLevel(pyrG, pyrL, level);
        }

        /// <summary>
        /// Create single Laplacian pyramid level based on existing Gaussian pyramid
        /// </summary>
        static void BuildLaplacianPyramidLevel(FloatBitmap[] pyrG, FloatBitmap[] pyrL, int level)
        {
            if (level < pyrG.Length - 1)
            {
                // Construct Laplacian pyramid level
                // Store difference between image and upsampled low pass version
                if (pyrL[level] == null)
                    pyrL[level] = new FloatBitmap(pyrG[level].Width, pyrG[level].Height, 1);
                BilinearResizeOperation.ApplyToPlaneParallel(pyrG[level + 1], pyrL[level]);
                pyrL[level].SubtractFrom(pyrG[level]);
            }
            else
                pyrL[level] = pyrG[level]; // The coarest level contains the residual low pass image
        }

        /// <summary>
        /// Create single Laplacian pyramid level based on existing Gaussian pyramid
        /// </summary>
        static void BuildLaplacianPyramidLevel(BitmapPlaneInt16[] pyrG, BitmapPlaneInt16[] pyrL, int level)
        {
            if (level < pyrG.Length - 1)
            {
                // Construct Laplacian pyramid level
                // Store difference between image and upsampled low pass version
                if (pyrL[level] == null)
                    pyrL[level] = new BitmapPlaneInt16(pyrG[level].Width, pyrG[level].Height);
                BilinearResizeOperation.ApplyToPlaneParallel(pyrG[level + 1], pyrL[level]);
                pyrL[level].SubtractFrom(pyrG[level]);
            }
            else
                pyrL[level] = pyrG[level]; // The coarest level contains the residual low pass image
        }

        /// <summary>
        /// Reconstruct image from Laplacian pyramid
        /// </summary>
        static void ReconstructLaplacianPyramid(FloatBitmap[] pyrL, FloatBitmap result)
        {
            int levels = pyrL.Length;
            // Start with low pass residual
            var R = pyrL[levels - 1];
            for (int level = levels - 2; level >= 0; level--)
            {
                // Upsample and add to current level
                FloatBitmap S;
                if (level == 0)
                    S = result;
                else
                    S = new FloatBitmap(pyrL[level].Width, pyrL[level].Height, 1);
                BilinearResizeOperation.ApplyToPlaneParallel(R, S, new ROI(0, pyrL[level].Width - 1, 0, pyrL[level].Height - 1));
                S.Add(pyrL[level]);
                R = S;
            }
        }

        /// <summary>
        /// Reconstruct image from Laplacian pyramid
        /// </summary>
        static void ReconstructLaplacianPyramid(BitmapPlaneInt16[] pyrL, BitmapPlaneInt16 result, BitmapPlaneInt16[] temp = null)
        {
            int levels = pyrL.Length;
            // Start with low pass residual
            var R = pyrL[levels - 1];
            for (int level = levels - 2; level >= 0; level--)
            {
                // Upsample, and add to current level
                BitmapPlaneInt16 S;
                if (level == 0)
                    S = result;
                else if (temp != null)
                    S = temp[level];
                else
                    S = new BitmapPlaneInt16(pyrL[level].Width, pyrL[level].Height);
                BilinearResizeOperation.ApplyToPlaneParallel(R, S, new ROI(0, pyrL[level].Width - 1, 0, pyrL[level].Height - 1));
                S.Add(pyrL[level]);
                R = S;
            }
        }
    }
}
