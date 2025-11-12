#nullable disable

using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    public class IncreaseLocalContrastOperation : OperationBase
    {
        FloatBitmap _minSourcePlane, _maxSourcePlane;
        FloatBitmap _minPlane, _maxPlane;
        bool _updateSourceMinMax, _updateMinMax;

        /// <summary>
        /// Outlier reduction in min/max planes.
        /// </summary>
        public float OutlierReductionFilterSize
        {
            get;
            set
            {
                if (value != field)
                {
                    field = value;
                    _updateSourceMinMax = true;
                    _updateMinMax = true;
                }
            }
        }

        public float LocalMinMaxFilterSize
        {
            get { return (_localMinFilterSize + _localMaxFilterSize) / 2; }
            set
            {
                if (value != _localMinFilterSize || value != _localMaxFilterSize)
                {
                    _localMinFilterSize = value;
                    _localMaxFilterSize = value;
                    _updateMinMax = true;
                }
            }
        }

        public float LocalMinFilterSize
        {
            get { return _localMinFilterSize; }
            set
            {
                if (value != _localMinFilterSize)
                {
                    _localMinFilterSize = value;
                    _updateMinMax = true;
                }
            }
        }
        private float _localMinFilterSize = 100f;

        public float LocalMaxFilterSize
        {
            get { return _localMaxFilterSize; }
            set
            {
                if (value != _localMaxFilterSize)
                {
                    _localMaxFilterSize = value;
                    _updateMinMax = true;
                }
            }
        }
        private float _localMaxFilterSize = 100f;

        public float MaxContrast
        {
            get;
            set
            {
                if (value != field)
                {
                    field = value;
                    _updateMinMax = true;
                }
            }
        } = 1f;

        public override void SourceChanged()
        {
            _updateMinMax = true;
            _updateSourceMinMax = true;
        }

        /// <summary>
        /// Enforce distance between min and max
        /// </summary>
        private void EnsureMinimumDifference()
        {
            unsafe
            {
                var requiredDiff = 1 - MaxContrast;
                Parallel.For(0, _minPlane.Height, y =>
                {
                    fixed (float* min = &_minPlane.Elements[y, 0])
                    fixed (float* max = &_maxPlane.Elements[y, 0])
                    {
                        var width = _minPlane.Width;
                        for (var x = 0; x < width; x++)
                        {
                            var minMaxDist = max[x] - min[x];
                            if (minMaxDist < requiredDiff)
                            {
                                var m = min[x] + (requiredDiff - minMaxDist) * min[x] / (minMaxDist - 1);
                                min[x] = m;
                                max[x] = m + requiredDiff;
                            }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Stretch contrast based on _minPlane and _maxPlane
        /// </summary>
        protected void LocalHistogramStretch()
        {
            DstBitmap.New(SrcBitmap.Width, SrcBitmap.Height, SrcBitmap.PlaneCount);
            unsafe
            {
                Parallel.For(0, SrcBitmap.Height, y =>
                {
                    int width = SrcBitmap.Width;
                    int planeCount = SrcBitmap.PlaneCount;
                    fixed (float* minPix = &_minPlane.Elements[y, 0])
                    fixed (float* maxPix = &_maxPlane.Elements[y, 0])
                    fixed (float* srcPix = &SrcBitmap.Elements[y, 0])
                    fixed (float* dstPix = &DstBitmap.Elements[y, 0])
                    {
                        int srcX = 0;
                        for (var x = 0; x < width; x++)
                        {
                            var diff = maxPix[x] - minPix[x];
                            if (diff > 1e-6f)
                            {
                                for (int p = 0; p < planeCount; p++)
                                {
                                    dstPix[srcX] = (srcPix[srcX] - minPix[x]) / diff;
                                    srcX++;
                                }
                            }
                            else
                            {
                                for (int p = 0; p < planeCount; p++)
                                {
                                    dstPix[srcX] = srcPix[srcX];
                                    srcX++;
                                }
                            }
                        }
                    }
                });
            }
        }

        private void UpdateSmoothSource()
        {
            _minSourcePlane ??= new FloatBitmap();
            _maxSourcePlane ??= new FloatBitmap();
            if (SrcBitmap.PlaneCount == 1)
            {
                _minSourcePlane.Assign(SrcBitmap);
                _maxSourcePlane.Assign(SrcBitmap);
            }
            else
            {
                _minSourcePlane.New(SrcBitmap.Width, SrcBitmap.Height, 1);
                _maxSourcePlane.New(SrcBitmap.Width, SrcBitmap.Height, 1);
                unsafe
                {
                    Parallel.For(0, SrcBitmap.Height, y =>
                    {
                        int width = SrcBitmap.Width;
                        int planeCount = SrcBitmap.PlaneCount;
                        fixed (float* srcRow = &SrcBitmap.Elements[y, 0])
                        fixed (float* minRow = &_minSourcePlane.Elements[y, 0])
                        fixed (float* maxRow = &_maxSourcePlane.Elements[y, 0])
                        {
                            int srcX = 0;
                            for (var x = 0; x < width; x++)
                            {
                                float min, max;
                                min = max = srcRow[srcX];
                                srcX++;
                                for (int p = 1; p < planeCount; p++)
                                {
                                    if (srcRow[srcX] < min)
                                        min = srcRow[srcX];
                                    if (srcRow[srcX] > max)
                                        max = srcRow[srcX];
                                    srcX++;
                                }
                                minRow[x] = min;
                                maxRow[x] = max;
                            }
                        }
                    });
                }
            }
            if (OutlierReductionFilterSize > 0)
                Parallel.Invoke(
                    () => IIRSmoothOperation.Apply(_minSourcePlane, OutlierReductionFilterSize),
                    () => IIRSmoothOperation.Apply(_maxSourcePlane, OutlierReductionFilterSize));
        }

        private void UpdateMinMaxIIR()
        {
            Parallel.Invoke(
            () =>
            {
                _minPlane ??= new FloatBitmap();
                if (LocalMinFilterSize > 0)
                {
                    _minPlane.Assign(_minSourcePlane);
                    IIRMinMaxOperation.MinFilter(_minPlane, LocalMinFilterSize);
                }
                else
                {
                    _minPlane.New(SrcBitmap.Width, SrcBitmap.Height, 1);
                    _minPlane.ProcessElementWise(p => 0);
                }
            },
            () =>
            {
                _maxPlane ??= new FloatBitmap();
                if (LocalMaxFilterSize > 0)
                {
                    _maxPlane.Assign(_maxSourcePlane);
                    IIRMinMaxOperation.MaxFilter(_maxPlane, LocalMaxFilterSize);
                }
                else
                {
                    _maxPlane.New(SrcBitmap.Width, SrcBitmap.Height, 1);
                    _maxPlane.ProcessElementWise(p => 1);
                }
            });
        }

        public override void Apply()
        {
            if (_updateSourceMinMax || _minSourcePlane is null || _maxSourcePlane is null)
            {
                _updateSourceMinMax = false;
                UpdateSmoothSource();
            }

            if (_updateMinMax || _minPlane == null || _maxPlane == null)
            {
                _updateMinMax = false;
                UpdateMinMaxIIR();
                if (MaxContrast < 1f)
                    EnsureMinimumDifference();
            }

            LocalHistogramStretch();
        }
    }
}
