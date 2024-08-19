using PhotoLocator.Helpers;
using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    class ColorToneAdjustOperation : OperationBase
    {
        public const int NumberOfTones = 8;

        FloatBitmap? _srcHSI;
        bool _updateSrcHsi;

        public struct ToneAdjustment(float toneHue)
        {
            public readonly float ToneHue = toneHue;
            public float AdjustHue = 0;
            public float AdjustSaturation = 1;
            public float AdjustIntensity = 1;
            public float HueUniformity = 0;
        }

        public ToneAdjustment[] ToneAdjustments { get; } = new ToneAdjustment[NumberOfTones];

        public float Rotation
        {
            get => _rotation;
            set
            {
                if (value <= -1 || value >= 1)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _rotation = value;
            }
        }
        private float _rotation;

        public ColorToneAdjustOperation()
        {
            ResetToneAdjustments();
        }

        public void ResetToneAdjustments()
        {
            for (var i = 0; i < NumberOfTones; i++)
                ToneAdjustments[i] = new ToneAdjustment((float)i / NumberOfTones);
        }

        public bool AreToneAdjustmentsChanged
        {
            get
            {
                for (var i = 0; i < NumberOfTones; i++)
                    if (ToneAdjustments[i].AdjustHue != 0 ||
                        ToneAdjustments[i].AdjustSaturation != 1 ||
                        ToneAdjustments[i].AdjustIntensity != 1 ||
                        ToneAdjustments[i].HueUniformity != 0)
                        return true;
                return false;
            }
        }

        public override void SourceChanged()
        {
            _updateSrcHsi = true;
        }

        public static void ColorTransformRGB2HSI(float r, float g, float b, out float h, out float x, out float i)
        {
            r = RealMath.EnsureRange(r, 0f, 1f);
            g = RealMath.EnsureRange(g, 0f, 1f);
            b = RealMath.EnsureRange(b, 0f, 1f);
            i = (r + g + b) / 3f;
            if (i == 0)
            {
                x = 0;
                h = 0;
            }
            else
            {
                var D = r;
                if (g < D)
                    D = g;
                if (b < D)
                    D = b;
                x = Math.Max(0, 1 - 3f / (r + g + b) * D);
                if (x == 0)
                    h = 0;
                else
                {
                    var a = 0.5 * (r - g + (r - b)) / Math.Sqrt(RealMath.Sqr(r - g) + (r - b) * (g - b));
                    double rh;
                    if (a <= -1)
                        rh = Math.PI;
                    else if (!(a < 1))
                        rh = 0;
                    else
                        rh = Math.Acos(a);
                    if (b > g)
                        rh = 2 * Math.PI - rh;
                    h = (float)(rh * (1 / (2 * Math.PI)));
                }
            }
        }

        public static void ColorTransformHSI2RGB(float h, float s, float i, out float r, out float g, out float b)
        {
            if (h > 1)
                h -= 1;
            else if (h < 0)
                h += 1;
            double rr, rg, rb, rh;
            if (h <= 1f / 3) // 0°<H<=120°
            {
                rh = h * (Math.PI * 2);
                rb = 1 - s;
                rr = 1 + s * Math.Cos(rh) / Math.Cos(Math.PI * 60 / 180 - rh);
                rg = 3 - (rr + rb);
            }
            else if (h < 2f / 3) // 120°<H<=240°
            {
                rh = (h - 1f / 3) * (Math.PI * 2);
                rr = 1 - s;
                rg = 1 + s * Math.Cos(rh) / Math.Cos(Math.PI * 60 / 180 - rh);
                rb = 3 - (rr + rg);
            }
            else // 240°<H<=360°
            {
                rh = (h - 2f / 3) * (Math.PI * 2);
                rg = 1 - s;
                rb = 1 + s * Math.Cos(rh) / Math.Cos(Math.PI * 60 / 180 - rh);
                rr = 3 - (rb + rg);
            }
            r = (float)(rr * i);
            if (r > 1)
                r = 1;
            g = (float)(rg * i);
            if (g > 1)
                g = 1;
            b = (float)(rb * i);
            if (b > 1)
                b = 1;
        }

        public static FloatBitmap ColorTransformRGB2HSI(FloatBitmap source, FloatBitmap destination)
        {
            destination.New(source.Width, source.Height, 3);
            Parallel.For(0, source.Height, y =>
            {
                unsafe
                {
                    fixed (float* src = &source.Elements[y, 0])
                    fixed (float* dst = &destination.Elements[y, 0])
                    {
                        int xx = 0;
                        for (var x = 0; x < source.Width; x++)
                        {
                            ColorTransformRGB2HSI(src[xx], src[xx + 1], src[xx + 2], out dst[xx], out dst[xx + 1], out dst[xx + 2]);
                            xx += 3;
                        }
                    }
                }
            });
            return destination;
        }

        public override void Apply()
        {
            if (SrcBitmap.PlaneCount != 3)
                throw new UserMessageException("Only RGB color images supported");
            if (_updateSrcHsi || _srcHSI is null)
            {
                _updateSrcHsi = false;
                _srcHSI ??= new FloatBitmap();
                ColorTransformRGB2HSI(SrcBitmap, _srcHSI);
            }
            DstBitmap.New(_srcHSI.Width, _srcHSI.Height, 3);
            Parallel.For(0, _srcHSI.Height, y =>
            {
                unsafe
                {
                    fixed (float* src = &_srcHSI.Elements[y, 0])
                    fixed (float* dst = &DstBitmap.Elements[y, 0])
                    {
                        int xx = 0;
                        for (var x = 0; x < _srcHSI.Width; x++)
                        {
                            var tone = (src[xx] - Rotation) * NumberOfTones;
                            if (tone < 0)
                                tone += NumberOfTones;
                            if (tone >= NumberOfTones)
                                tone -= NumberOfTones;
                            var toneIndex = (int)tone;
                            var nextToneIndex = toneIndex + 1;
                            if (nextToneIndex == NumberOfTones)
                                nextToneIndex = 0;
                            var nextToneWeight = RealMath.SmoothStep(tone - toneIndex);
                            var toneWeight = 1 - nextToneWeight;

                            var hue = src[xx];
                            if (ToneAdjustments[toneIndex].HueUniformity > 0 || ToneAdjustments[nextToneIndex].HueUniformity > 0)
                            {
                                var toneHue = ToneAdjustments[toneIndex].ToneHue + Rotation;
                                if (toneHue < hue - 0.5f)
                                    toneHue++;
                                else if (toneHue > hue + 0.5f)
                                    toneHue--;
                                var nextToneHue = ToneAdjustments[nextToneIndex].ToneHue + Rotation;
                                if (nextToneHue < hue - 0.5f)
                                    nextToneHue++;
                                else if (nextToneHue > hue + 0.5f)
                                    nextToneHue--;

                                var toneHueWeight = ToneAdjustments[toneIndex].HueUniformity * toneWeight;
                                var nextToneHueWeight = ToneAdjustments[nextToneIndex].HueUniformity * nextToneWeight;
                                hue = hue * (1 - toneHueWeight - nextToneHueWeight) +
                                    toneHue * toneHueWeight +
                                    nextToneHue * nextToneHueWeight;
                            }
                            var h = hue +
                                ToneAdjustments[toneIndex].AdjustHue * toneWeight +
                                ToneAdjustments[nextToneIndex].AdjustHue * nextToneWeight;
                            var s = src[xx + 1] *
                                (1 - toneWeight + ToneAdjustments[toneIndex].AdjustSaturation * toneWeight) *
                                (1 - nextToneWeight + ToneAdjustments[nextToneIndex].AdjustSaturation * nextToneWeight);
                            if (s > 1)
                                s = 1;
                            toneWeight *= s;
                            nextToneWeight *= s;
                            var i = src[xx + 2] *
                                (1 - toneWeight + ToneAdjustments[toneIndex].AdjustIntensity * toneWeight) *
                                (1 - nextToneWeight + ToneAdjustments[nextToneIndex].AdjustIntensity * nextToneWeight);
                            ColorTransformHSI2RGB(h, s, i, out dst[xx], out dst[xx + 1], out dst[xx + 2]);
                            xx += 3;
                        }
                    }
                }
            });
        }
    }
}
