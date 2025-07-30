using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    class TimeSliceOperation
    {
        const int PixelSize = 3;

        readonly List<byte[]> _frames = [];
        int _width, _height;
        double _dpiX, _dpiY;
        PixelFormat _pixelFormat;
        int _takeFrameInterval = 1;
        int _maxFrames, _addedFrames;

        public SelectionMapFunction? SelectionMapExpression { get; set; }

        public FloatBitmap? SelectionMap { get; set; }

        public int UsedFrames => _frames.Count;

        public int SkippedFrames => _addedFrames - UsedFrames;

        public void AddFrame(BitmapSource image)
        {
            if (_width == 0)
            {
                _width = image.PixelWidth;
                _height = image.PixelHeight;
                _dpiX = image.DpiX;
                _dpiY = image.DpiY;
                _pixelFormat = image.Format;
                if (image.Format != PixelFormats.Rgb24 && image.Format != PixelFormats.Bgr24)
                    throw new UserMessageException("Unsupported pixel format: " + image.Format);

                var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                var frameSize = _width * _height * PixelSize;
                _maxFrames = (int)Math.Ceiling(availableMemory * 0.75 / frameSize);
            }
            else if (_width != image.PixelWidth || _height != image.PixelHeight || _pixelFormat != image.Format)
                throw new UserMessageException("All frames must have the same dimensions and format.");

            if (_addedFrames % _takeFrameInterval == 0)
            {
                var pixels = new byte[_width * _height * PixelSize];
                image.CopyPixels(pixels, _width * PixelSize, 0);
                _frames.Add(pixels);

                if (_frames.Count > _maxFrames) // Remove every second frame to limit memory usage
                {
                    _takeFrameInterval *= 2;
                    for (int i = _frames.Count - 1; i > 0; i -= 2)
                        _frames.RemoveAt(i);
                }
            }
            _addedFrames++;
        }

        FloatBitmap GenerateSelectionMap()
        {
            if (_frames.Count == 0)
                throw new UserMessageException("No frames added to the time slice operation.");

            FloatBitmap selectionMap;
            if (SelectionMapExpression != null)
                selectionMap = TimeSliceSelectionMaps.GenerateSelectionMap(_width, _height, SelectionMapExpression);
            else
            {
                if (SelectionMap == null)
                    throw new UserMessageException("SelectionMap must be set before generating the time slice image.");
                if (SelectionMap.PlaneCount > 1)
                    throw new UserMessageException("SelectionMap must be a single plane bitmap.");
                selectionMap = new FloatBitmap(_width, _height, 1);
                BilinearResizeOperation.ApplyToPlaneParallel(SelectionMap, selectionMap);
            }

            var scale = _frames.Count - 1e-3f;
            selectionMap.ProcessElementWise(a => a * scale);
            return selectionMap;
        }

        public BitmapSource GenerateTimeSliceImage()
        {
            var selectionMap = GenerateSelectionMap();

            var resultPixels = new byte[_width * _height * PixelSize];
            Parallel.For(0, _height, y =>
            {
                unsafe
                {
                    int pixIndex = y * _width * PixelSize;
                    fixed (float* selectionRow = &selectionMap.Elements[y, 0])
                    fixed (byte* result = resultPixels)
                    {
                        for (int x = 0; x < _width; x++)
                        {
                            var frame = _frames[(int)selectionRow[x]];
                            result[pixIndex] = frame[pixIndex];
                            result[pixIndex + 1] = frame[pixIndex + 1];
                            result[pixIndex + 2] = frame[pixIndex + 2];
                            pixIndex += PixelSize;
                        }
                    }
                }
            });

            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource GenerateTimeSliceImageInterpolated()
        {
            var selectionMap = GenerateSelectionMap();

            int maxIndex = _frames.Count - 1;
            var resultPixels = new byte[_width * _height * PixelSize];
            Parallel.For(0, _height, y =>
            {
                unsafe
                {
                    int pixIndex = y * _width * PixelSize;
                    fixed (float* selectionRow = &selectionMap.Elements[y, 0])
                    fixed (byte* result = resultPixels)
                    {
                        for (int x = 0; x < _width; x++)
                        {
                            var selected = selectionRow[x];
                            var frameIndex = (int)selected;
                            int nextFrameIndex;
                            float nextWeight;
                            if (frameIndex < maxIndex)
                            {
                                nextFrameIndex = frameIndex + 1;
                                nextWeight = selected - frameIndex;
                            }
                            else
                            {
                                
                                nextFrameIndex = frameIndex;
                                nextWeight = 0; // No interpolation needed for the last frame
                            }
                            var frame = _frames[frameIndex];
                            var nextFrame = _frames[nextFrameIndex];
                            result[pixIndex] = (byte)Math.Round(frame[pixIndex] * (1 - nextWeight) + nextFrame[pixIndex] * nextWeight);
                            result[pixIndex + 1] = (byte)Math.Round(frame[pixIndex + 1] * (1 - nextWeight) + nextFrame[pixIndex + 1] * nextWeight);
                            result[pixIndex + 2] = (byte)Math.Round(frame[pixIndex + 2] * (1 - nextWeight) + nextFrame[pixIndex + 2] * nextWeight);
                            pixIndex += PixelSize;
                        }
                    }
                }
            });

            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
            result.Freeze();
            return result;
        }

        public IEnumerable<BitmapSource> GenerateTimeSliceVideo(int loops = 1)
        {
            var selectionMap = GenerateSelectionMap();

            var resultPixels = new byte[_width * _height * PixelSize];
            for (int loop = 0; loop < loops; loop++)
            for (int i = 0; i < _frames.Count; i++)
            {
                Parallel.For(0, _height, y =>
                {
                    unsafe
                    {
                        int pixIndex = y * _width * PixelSize;
                        fixed (float* selectionRow = &selectionMap.Elements[y, 0])
                        fixed (byte* result = resultPixels)
                        {
                            for (int x = 0; x < _width; x++)
                            {
                                var frame = _frames[((int)selectionRow[x] + i) % _frames.Count];
                                result[pixIndex] = frame[pixIndex];
                                result[pixIndex + 1] = frame[pixIndex + 1];
                                result[pixIndex + 2] = frame[pixIndex + 2];
                                pixIndex += PixelSize;
                            }
                        }
                    }
                });

                var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
                result.Freeze();
                yield return result;
            }
        }

        public IEnumerable<BitmapSource> GenerateTimeSliceVideoInterpolated(int loops = 1)
        {
            var selectionMap = GenerateSelectionMap();

            int maxIndex = _frames.Count - 1;
            var resultPixels = new byte[_width * _height * PixelSize];
            for (int loop = 0; loop < loops; loop++)
            for (int i = 0; i < _frames.Count; i++)
            {
                Parallel.For(0, _height, y =>
                {
                    unsafe
                    {
                        int pixIndex = y * _width * PixelSize;
                        fixed (float* selectionRow = &selectionMap.Elements[y, 0])
                        fixed (byte* result = resultPixels)
                        {
                            for (int x = 0; x < _width; x++)
                            {
                                var selected = selectionRow[x];
                                var frameIndex = (int)selected;
                                int nextFrameIndex;
                                var nextWeight = selected - frameIndex;
                                frameIndex = (frameIndex + i) % _frames.Count; // Wrap around to allow looping through frames
                                if (frameIndex < maxIndex)
                                {
                                    nextFrameIndex = frameIndex + 1;
                                }
                                else
                                {
                                    nextFrameIndex = frameIndex;
                                    nextWeight = 0; // No interpolation needed for the last frame
                                }
                                var frame = _frames[frameIndex];
                                var nextFrame = _frames[nextFrameIndex];
                                result[pixIndex] = (byte)Math.Round(frame[pixIndex] * (1 - nextWeight) + nextFrame[pixIndex] * nextWeight);
                                result[pixIndex + 1] = (byte)Math.Round(frame[pixIndex + 1] * (1 - nextWeight) + nextFrame[pixIndex + 1] * nextWeight);
                                result[pixIndex + 2] = (byte)Math.Round(frame[pixIndex + 2] * (1 - nextWeight) + nextFrame[pixIndex + 2] * nextWeight);
                                pixIndex += PixelSize;
                            }
                        }
                    }
                });

                var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
                result.Freeze();
                yield return result;
            }
        }
    }
}
