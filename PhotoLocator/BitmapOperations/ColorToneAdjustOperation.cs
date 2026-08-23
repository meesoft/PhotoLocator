using PhotoLocator.Helpers;
using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    class ColorToneAdjustOperation : OperationBase
    {
        public const int NumberOfHues = 8;

        public const int NumberOfTones = NumberOfHues * 2;

        public const float ToneLowSaturation = 0.4f;
        public const float ToneHighSaturation = 0.8f;

        FloatBitmap? _srcHSI;
        bool _updateSrcHsi;

        public struct ToneAdjustment(float toneHue, float toneSaturation)
        {
            public readonly float ToneHue = toneHue;
            public readonly float ToneSaturation = toneSaturation;
            public float AdjustHue = 0;
            public float AdjustSaturation = 1;
            public float AdjustIntensity = 1;
            public float HueUniformity = 0;
        }

        public ToneAdjustment[] ToneAdjustments { get; } = new ToneAdjustment[NumberOfTones];

        public float Rotation
        {
            get;
            set
            {
                if (value <= -1 || value >= 1)
                    throw new ArgumentOutOfRangeException(nameof(value));
                field = value;
            }
        }

        public ColorToneAdjustOperation()
        {
            ResetToneAdjustments();
        }

        public void ResetToneAdjustments()
        {
            for (var i = 0; i < NumberOfHues; i++)
            {
                ToneAdjustments[i] = new ToneAdjustment((float)i / NumberOfHues, ToneLowSaturation);
                ToneAdjustments[i + NumberOfHues] = new ToneAdjustment((float)i / NumberOfHues, ToneHighSaturation);
            }
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

        public static void ColorTransformRGB2HSI(float r, float g, float b, out float h, out float s, out float i)
        {
            r = Math.Clamp(r, 0f, 1f);
            g = Math.Clamp(g, 0f, 1f);
            b = Math.Clamp(b, 0f, 1f);
            i = (r + g + b) / 3f;
            if (i == 0)
            {
                s = 0;
                h = 0;
            }
            else
            {
                var min = r;
                if (g < min)
                    min = g;
                if (b < min)
                    min = b;
                s = Math.Max(0, 1 - 3f / (r + g + b) * min);
                if (s == 0)
                    h = 0;
                else
                {
                    var a = 0.5 * (r - g + (r - b)) / Math.Sqrt((r - g) * (r - g) + (r - b) * (g - b));
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

        public static FloatBitmap ColorTransformRGB2HSI(FloatBitmap source, FloatBitmap destination)
        {
            destination.New(source.Width, source.Height, 3);
            Parallel.For(0, source.Height, y =>
            {
                unsafe
                {
                    var width = source.Width;
                    fixed (float* src = &source.Elements[y, 0])
                    fixed (float* dst = &destination.Elements[y, 0])
                    {
                        int xx = 0;
                        for (var x = 0; x < width; x++)
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
                var toneAdjustments = ToneAdjustments;
                var width = _srcHSI.Width;
                unsafe
                {
                    fixed (float* src = &_srcHSI.Elements[y, 0])
                    fixed (float* dst = &DstBitmap.Elements[y, 0])
                    {
                        int xx = 0;
                        for (var x = 0; x < width; x++)
                        {
                            var hueTone = (src[xx] - Rotation) * NumberOfHues;
                            if (hueTone < 0)
                                hueTone += NumberOfHues;
                            if (hueTone >= NumberOfHues)
                                hueTone -= NumberOfHues;
                            var hueIndex = (int)hueTone;
                            var nextHueIndex = hueIndex + 1;
                            if (nextHueIndex == NumberOfHues)
                                nextHueIndex = 0;
                            var nextHueWeight = RealMath.SmoothStep(hueTone - hueIndex);
                            var hueWeight = 1 - nextHueWeight;

                            var saturation = src[xx + 1];
                            float saturationWeight, nextSaturationWeight;
                            if (saturation <= ToneLowSaturation)
                            {
                                saturationWeight = 1; nextSaturationWeight = 0;
                            }
                            else if (saturation >= ToneHighSaturation)
                            {
                                saturationWeight = 0; nextSaturationWeight = 1;
                            }
                            else
                            {
                                var saturationTone = (saturation - ToneLowSaturation) / (ToneHighSaturation - ToneLowSaturation);
                                nextSaturationWeight = RealMath.SmoothStep(saturationTone);
                                saturationWeight = 1 - nextSaturationWeight;
                            }

                            var hue = src[xx];
                            if (toneAdjustments[hueIndex].HueUniformity > 0 || toneAdjustments[nextHueIndex].HueUniformity > 0)
                            {
                                var toneHue = toneAdjustments[hueIndex].ToneHue + Rotation;
                                if (toneHue < hue - 0.5f)
                                    toneHue++;
                                else if (toneHue > hue + 0.5f)
                                    toneHue--;
                                var nextToneHue = toneAdjustments[nextHueIndex].ToneHue + Rotation;
                                if (nextToneHue < hue - 0.5f)
                                    nextToneHue++;
                                else if (nextToneHue > hue + 0.5f)
                                    nextToneHue--;

                                var toneHueWeight = toneAdjustments[hueIndex].HueUniformity * hueWeight;
                                var nextToneHueWeight = toneAdjustments[nextHueIndex].HueUniformity * nextHueWeight;
                                hue = hue * (1 - toneHueWeight - nextToneHueWeight) +
                                    toneHue * toneHueWeight +
                                    nextToneHue * nextToneHueWeight;
                            }
                            var h = hue +
                                (toneAdjustments[hueIndex].AdjustHue * saturationWeight + toneAdjustments[hueIndex + NumberOfHues].AdjustHue * nextSaturationWeight) * hueWeight +
                                (toneAdjustments[nextHueIndex].AdjustHue * saturationWeight + toneAdjustments[nextHueIndex + NumberOfHues].AdjustHue * nextSaturationWeight) * nextHueWeight;
                            var s = src[xx + 1] *
                                ((toneAdjustments[hueIndex].AdjustSaturation * saturationWeight + toneAdjustments[hueIndex + NumberOfHues].AdjustSaturation * nextSaturationWeight) * hueWeight +
                                 (toneAdjustments[nextHueIndex].AdjustSaturation * saturationWeight + toneAdjustments[nextHueIndex + NumberOfHues].AdjustSaturation * nextSaturationWeight) * nextHueWeight);
                            if (s > 1)
                                s = 1;
                            var i = src[xx + 2] *
                                ((toneAdjustments[hueIndex].AdjustIntensity * saturationWeight + toneAdjustments[hueIndex + NumberOfHues].AdjustIntensity * nextSaturationWeight) * hueWeight +
                                 (toneAdjustments[nextHueIndex].AdjustIntensity * saturationWeight + toneAdjustments[nextHueIndex + NumberOfHues].AdjustIntensity * nextSaturationWeight) * nextHueWeight);
                            ColorTransformHSI2RGB(h, s, i, out dst[xx], out dst[xx + 1], out dst[xx + 2]);
                            xx += 3;
                        }
                    }
                }
            });
        }
    }
}
