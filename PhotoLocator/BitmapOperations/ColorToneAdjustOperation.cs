using PhotoLocator.Helpers;
using System;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations
{
    class ColorToneAdjustOperation : OperationBase
    {
        public const int NumberOfHues = 8;

        const float SingleToneSaturation = 0.5f;
        const float ToneLowSaturation = 0.4f;
        const float ToneHighSaturation = 0.9f;

        FloatBitmap? _srcHSI;
        bool _updateSrcHsi;

        public struct ToneAdjustment(float toneHue, float toneSaturation)
        {
            public float ToneHue { get; } = toneHue;
            public float ToneSaturation { get; internal set; } = toneSaturation;
            public float AdjustHue = 0;
            public float AdjustSaturation = 1;
            public float AdjustIntensity = 1;
            public float HueUniformity = 0;
        }
       
        public int NumberOfTones => ToneAdjustments.Length;

        public ToneAdjustment[] ToneAdjustments { get; private set; } = new ToneAdjustment[NumberOfHues];

        public float Rotation
        {
            get;
            set
            {
                if (value is <= -1 or >= 1)
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
            if (DualToneMode)
                for (var i = 0; i < NumberOfHues; i++)
                {
                    ToneAdjustments[i] = new ToneAdjustment((float)i / NumberOfHues, ToneLowSaturation);
                    ToneAdjustments[i + NumberOfHues] = new ToneAdjustment((float)i / NumberOfHues, ToneHighSaturation);
                }
            else
                for (var i = 0; i < NumberOfHues; i++)
                    ToneAdjustments[i] = new ToneAdjustment((float)i / NumberOfHues, SingleToneSaturation);
        }

        public bool DualToneMode
        {
            get => ToneAdjustments.Length > NumberOfHues;
            set
            {
                if (value != DualToneMode)
                {
                    if (value)
                    {
                        var newToneAdjustments = new ToneAdjustment[NumberOfHues * 2];
                        Array.Copy(ToneAdjustments, newToneAdjustments, NumberOfHues);
                        Array.Copy(ToneAdjustments, 0, newToneAdjustments, NumberOfHues, NumberOfHues);
                        for (var i = 0; i < NumberOfHues; i++)
                        {
                            newToneAdjustments[i].ToneSaturation = ToneLowSaturation;
                            newToneAdjustments[i + NumberOfHues].ToneSaturation = ToneHighSaturation;
                        }
                        ToneAdjustments = newToneAdjustments;
                    }
                    else
                    {
                        var newToneAdjustments = new ToneAdjustment[NumberOfHues];
                        Array.Copy(ToneAdjustments, newToneAdjustments, NumberOfHues);
                        for (var i = 0; i < NumberOfHues; i++)
                            newToneAdjustments[i].ToneSaturation = SingleToneSaturation;
                        ToneAdjustments = newToneAdjustments;
                    }
                }
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
            if (s > 1)
                s = 1;
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
            if (DualToneMode)
                ApplyDualToneAdjustments();
            else
                ApplySingleToneAdjustments();
        }

        void ApplySingleToneAdjustments()
        {
            Parallel.For(0, _srcHSI!.Height, y =>
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
                            var tone = (src[xx] - Rotation) * NumberOfHues;
                            if (tone < 0)
                                tone += NumberOfHues;
                            if (tone >= NumberOfHues)
                                tone -= NumberOfHues;
                            var toneIndex = (int)tone;
                            var nextToneIndex = toneIndex + 1;
                            if (nextToneIndex == NumberOfHues)
                                nextToneIndex = 0;
                            var nextToneWeight = RealMath.SmoothStep(tone - toneIndex);
                            var toneWeight = 1 - nextToneWeight;

                            var hue = src[xx];
                            if (toneAdjustments[toneIndex].HueUniformity > 0 || toneAdjustments[nextToneIndex].HueUniformity > 0)
                            {
                                var toneHue = toneAdjustments[toneIndex].ToneHue + Rotation;
                                if (toneHue < hue - 0.5f)
                                    toneHue++;
                                else if (toneHue > hue + 0.5f)
                                    toneHue--;
                                var nextToneHue = toneAdjustments[nextToneIndex].ToneHue + Rotation;
                                if (nextToneHue < hue - 0.5f)
                                    nextToneHue++;
                                else if (nextToneHue > hue + 0.5f)
                                    nextToneHue--;

                                var toneHueWeight = toneAdjustments[toneIndex].HueUniformity * toneWeight;
                                var nextToneHueWeight = toneAdjustments[nextToneIndex].HueUniformity * nextToneWeight;
                                hue = hue * (1 - toneHueWeight - nextToneHueWeight) +
                                    toneHue * toneHueWeight +
                                    nextToneHue * nextToneHueWeight;
                            }
                            var ha = toneAdjustments[toneIndex].AdjustHue;
                            var haNext = FixHue(toneAdjustments[nextToneIndex].AdjustHue, ha);
                            double hueVectorX = 0;
                            double hueVectorY = 0;
                            AddHueVector(hue + ha, toneAdjustments[toneIndex].AdjustSaturation, toneWeight, ref hueVectorX, ref hueVectorY);
                            AddHueVector(hue + haNext, toneAdjustments[nextToneIndex].AdjustSaturation, nextToneWeight, ref hueVectorX, ref hueVectorY);
                            GetHueAndSaturation(hueVectorX, hueVectorY, hue, out var h, out var saturationAdjust);

                            var s = src[xx + 1] * saturationAdjust;

                            var i = src[xx + 2] *
                                (toneAdjustments[toneIndex].AdjustIntensity * toneWeight +
                                 toneAdjustments[nextToneIndex].AdjustIntensity * nextToneWeight);

                            ColorTransformHSI2RGB(h, s, i, out dst[xx], out dst[xx + 1], out dst[xx + 2]);
                            xx += 3;
                        }
                    }
                }
            });
        }

        void ApplyDualToneAdjustments()
        { 
            Parallel.For(0, _srcHSI!.Height, y =>
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
                            float lowSaturationWeight, highSaturationWeight;
                            if (saturation <= ToneLowSaturation)
                            {
                                lowSaturationWeight = 1; highSaturationWeight = 0;
                            }
                            else if (saturation >= ToneHighSaturation)
                            {
                                lowSaturationWeight = 0; highSaturationWeight = 1;
                            }
                            else
                            {
                                var saturationTone = (saturation - ToneLowSaturation) / (ToneHighSaturation - ToneLowSaturation);
                                highSaturationWeight = RealMath.SmoothStep(saturationTone);
                                lowSaturationWeight = 1 - highSaturationWeight;
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

                            var haLow = toneAdjustments[hueIndex].AdjustHue;
                            var haHigh = FixHue(toneAdjustments[hueIndex + NumberOfHues].AdjustHue, haLow);
                            var haLowNext = FixHue(toneAdjustments[nextHueIndex].AdjustHue, haLow);
                            var haHighNext = FixHue(toneAdjustments[nextHueIndex + NumberOfHues].AdjustHue, haLowNext);
                            double hueVectorX = 0;
                            double hueVectorY = 0;
                            AddHueVector(hue + haLow, toneAdjustments[hueIndex].AdjustSaturation, hueWeight * lowSaturationWeight, ref hueVectorX, ref hueVectorY);
                            AddHueVector(hue + haHigh, toneAdjustments[hueIndex + NumberOfHues].AdjustSaturation, hueWeight * highSaturationWeight, ref hueVectorX, ref hueVectorY);
                            AddHueVector(hue + haLowNext, toneAdjustments[nextHueIndex].AdjustSaturation, nextHueWeight * lowSaturationWeight, ref hueVectorX, ref hueVectorY);
                            AddHueVector(hue + haHighNext, toneAdjustments[nextHueIndex + NumberOfHues].AdjustSaturation, nextHueWeight * highSaturationWeight, ref hueVectorX, ref hueVectorY);
                            GetHueAndSaturation(hueVectorX, hueVectorY, hue, out var h, out var saturationAdjust);

                            var s = src[xx + 1] * saturationAdjust;
                          
                            var i = src[xx + 2] *
                                ((toneAdjustments[hueIndex].AdjustIntensity * lowSaturationWeight + toneAdjustments[hueIndex + NumberOfHues].AdjustIntensity * highSaturationWeight) * hueWeight +
                                 (toneAdjustments[nextHueIndex].AdjustIntensity * lowSaturationWeight + toneAdjustments[nextHueIndex + NumberOfHues].AdjustIntensity * highSaturationWeight) * nextHueWeight);
                            
                            ColorTransformHSI2RGB(h, s, i, out dst[xx], out dst[xx + 1], out dst[xx + 2]);
                            xx += 3;
                        }
                    }
                }
            });
        }

        private static float FixHue(float hueAdjust, float hueAdjustBaseBase)
        {
            if (hueAdjust < hueAdjustBaseBase - 0.5f)
                hueAdjust += 1;
            else if (hueAdjust > hueAdjustBaseBase + 0.5f)
                hueAdjust -= 1;
            return hueAdjust;
        }

        private static void AddHueVector(float hue, float saturation, float weight, ref double x, ref double y)
        {
            var angle = hue * (Math.PI * 2);
            x += weight * saturation * Math.Cos(angle);
            y += weight * saturation * Math.Sin(angle);
        }

        private static void GetHueAndSaturation(double x, double y, float fallbackHue, out float hue, out float saturation)
        {
            saturation = (float)Math.Sqrt(x * x + y * y);
            if (saturation > 0)
            {
                hue = (float)(Math.Atan2(y, x) / (Math.PI * 2));
                if (hue < 0)
                    hue++;
            }
            else
                hue = fallbackHue;
        }
    }
}
