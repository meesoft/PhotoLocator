using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    /// <summary>
    /// Render a simple starfield (flying through stars) producing 8-bit grayscale frames.
    /// </summary>
    public class StarfieldRenderer
    {
        readonly int _width;
        readonly int _height;
        readonly int _centerX;
        readonly int _centerY;
        readonly Star[] _stars;
        readonly Random _rnd;
        readonly float _focal; // projection focal length
        readonly float _growthFactor;
        readonly float _speed;

        struct Star
        {
            public float X; // world X (centered)
            public float Y; // world Y (centered)
            public float Z; // depth in range (0..1], smaller = closer
        }

        /// <summary>
        /// Create a starfield renderer.
        /// </summary>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <param name="starCount">Number of stars to render.</param>
        /// <param name="speed">Per-frame approach speed. Typical small values like 0.01 - 0.1.</param>
        /// <param name="growthFactor">How much stars grow when they get close.</param>
        /// <param name="seed">Optional random seed for reproducible results.</param>
        public StarfieldRenderer(int width, int height, int starCount, float speed = 0.02f, float growthFactor = 1.5f, int? seed = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(starCount);

            _width = width;
            _height = height;
            _centerX = width / 2;
            _centerY = height / 2;
            _stars = new Star[starCount];
            _rnd = seed.HasValue ? new Random(seed.Value) : new Random();
            _focal = Math.Max(width, height) * 0.5f; // reasonable focal length
            _growthFactor = Math.Max(0.1f, growthFactor);
            _speed = Math.Max(0f, speed);

            InitializeStars();
        }

        void InitializeStars()
        {
            for (int i = 0; i < _stars.Length; i++)
                _stars[i] = CreateStar();
        }

        Star CreateStar()
        {
            // World coordinates centered around 0. X range roughly [-width/2, width/2]
#pragma warning disable CA5394 // Do not use insecure randomness
            var x = (float)(_rnd.NextDouble() * _width - _centerX);
            var y = (float)(_rnd.NextDouble() * _height - _centerY);
            // Z in (0.05 .. 1], avoid zero
            var z = (float)(_rnd.NextDouble() * 0.95 + 0.05);
#pragma warning restore CA5394 // Do not use insecure randomness
            return new Star { X = x, Y = y, Z = z };
        }

        /// <summary>
        /// Generate a sequence of frames. Each yielded BitmapSource is frozen and is an 8-bit grayscale image.
        /// </summary>
        /// <param name="frameCount">Number of frames to generate.</param>
        public IEnumerable<BitmapSource> GenerateFrames(int frameCount)
        {
            for (int f = 0; f < frameCount; f++)
            {
                // create fresh pixel buffer for this frame
                var pixels = new byte[_width * _height * 3];

                // update stars and draw
                for (int i = 0; i < _stars.Length; i++)
                {
                    var s = _stars[i];

                    // Move star closer by decreasing Z
                    s.Z -= _speed;
                    if (s.Z <= 0.02f)
                    {
                        // respawn at far distance
                        s = CreateStar();
                        s.Z = 1.0f;
                    }

                    // projection
                    var invZ = 1f / s.Z;
                    var sx = IntMath.Round(_centerX + s.X * invZ * (_focal / _centerX));
                    var sy = IntMath.Round(_centerY + s.Y * invZ * (_focal / _centerY));

                    // compute intensity and radius
                    // closer stars (smaller Z) are brighter and bigger
                    var brightness = (1f - s.Z) * 255f;
                    var b = Math.Clamp((int)brightness, 0, 255);
                    var radius = Math.Max(0.0f, (1f - s.Z) * _growthFactor * 2.0f);
                    Debug.Assert(radius >= 0f);
                    var r = (int)radius;

                    // draw filled circle
                    if (sx >= -r && sx < _width + r && sy >= -r && sy < _height + r)
                    {
                        for (int yy = -r; yy <= r; yy++)
                        {
                            int py = sy + yy;
                            if (py < 0 || py >= _height) continue;
                            int dx = (int)Math.Floor(Math.Sqrt(radius * radius - yy * yy));
                            int x0 = Math.Max(0, sx - dx);
                            int x1 = Math.Min(_width - 1, sx + dx);
                            int baseIndex = py * _width * 3;
                            for (int px = x0; px <= x1; px++)
                            {
                                int idx = baseIndex + px * 3;
                                // additive blending clamped to 255
                                var val = (byte)Math.Min(pixels[idx] + b, 255);
                                pixels[idx] = val;
                                pixels[idx + 1] = val;
                                pixels[idx + 2] = val;
                            }
                        }
                    }

                    _stars[i] = s;
                }

                var bmp = BitmapSource.Create(_width, _height, 96, 96, PixelFormats.Rgb24, null, pixels, _width * 3);
                bmp.Freeze();
                yield return bmp;
            }
        }

        internal static BitmapSource AddFrames(BitmapSource frame1, BitmapSource frame2)
        {
            var buf1 = new byte[frame1.PixelWidth * frame1.PixelHeight * 3];
            var buf2 = new byte[frame2.PixelWidth * frame2.PixelHeight * 3];
            frame1.CopyPixels(buf1, frame1.PixelWidth * 3, 0);
            frame2.CopyPixels(buf2, frame1.PixelWidth * 3, 0);
            Parallel.For(0, buf1.Length, i => buf1[i] = (byte)Math.Min(buf1[i] + buf2[i], 255));
            var result = BitmapSource.Create(frame1.PixelWidth, frame1.PixelHeight, 96, 96, frame1.Format, null, buf1, frame1.PixelWidth * 3);
            result.Freeze();
            return result;
        }
    }
}
