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

        public SelectionMapFunction? SelectionMapExpression { get; set; }

        public FloatBitmap? SelectionMap { get; set; }

        public int NumberOfFrames => _frames.Count;

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
            }
            else if (_width != image.PixelWidth || _height != image.PixelHeight || _pixelFormat != image.Format)
                throw new UserMessageException("All frames must have the same dimensions and format.");

            var pixels = new byte[_width * _height * PixelSize];
            image.CopyPixels(pixels, _width * PixelSize, 0);
            _frames.Add(pixels);
        }

        FloatBitmap GenerateSelectionMap()
        {
            if (_frames.Count == 0)
                throw new UserMessageException("No frames added to the time slice operation.");

            if (SelectionMapExpression != null)
                return TimeSliceSelectionMaps.GenerateSelectionMap(_width, _height, SelectionMapExpression);

            if (SelectionMap == null)
                throw new UserMessageException("SelectionMap must be set before generating the time slice image.");
            if (SelectionMap.PlaneCount > 1)
                throw new UserMessageException("SelectionMap must be a single plane bitmap.");

            var selectionMap = new FloatBitmap(_width, _height, 1);
            BilinearResizeOperation.ApplyToPlaneParallel(SelectionMap, selectionMap);
            return selectionMap;
        }

        public BitmapSource GenerateTimeSliceImage()
        {
            var selectionMap = GenerateSelectionMap();
            //selectionMap.SaveToFile("z:\\SelectionMap.png");

            int maxIndex = _frames.Count - 1;
            selectionMap.ProcessElementWise(a => a * maxIndex);

            var resultPixels = new byte[_width * _height * PixelSize];
            for (int i = 0; i < _frames.Count; i++)
            {
                Parallel.For(0, _height, y =>
                {
                    unsafe
                    {
                        fixed (float* selectionRow = &selectionMap.Elements[y, 0])
                        fixed (byte* frameRow = &_frames[i][y * _width * PixelSize])
                        fixed (byte* resultRow = &resultPixels[y * _width * PixelSize])
                        {
                            for (int x = 0; x < _width; x++)
                            {
                                var sourceFrameIndex = IntMath.Round(selectionRow[x]);
                                if (sourceFrameIndex == i)
                                {
                                    resultRow[x * PixelSize] = frameRow[x * PixelSize];
                                    resultRow[x * PixelSize + 1] = frameRow[x * PixelSize + 1];
                                    resultRow[x * PixelSize + 2] = frameRow[x * PixelSize + 2];
                                }
                            }
                        }
                    }
                });
            }

            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
            result.Freeze();
            return result;
        }

        public BitmapSource GenerateTimeSliceImageInterpolated()
        {
            var selectionMap = GenerateSelectionMap();
            //selectionMap.SaveToFile("z:\\SelectionMap.png");

            int maxIndex = _frames.Count - 1;
            selectionMap.ProcessElementWise(a => a * maxIndex);

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
                            float weight;
                            if (frameIndex < maxIndex)
                            {
                                nextFrameIndex = frameIndex + 1;
                                weight = selected - frameIndex;
                            }
                            else
                            {
                                
                                nextFrameIndex = frameIndex;
                                weight = 0; // No interpolation needed for the last frame
                            }
                            var frame= _frames[frameIndex];
                            var nextFrame = _frames[nextFrameIndex];
                            result[pixIndex] = (byte)Math.Round(frame[pixIndex] * (1 - weight) + nextFrame[pixIndex] * weight);
                            result[pixIndex + 1] = (byte)Math.Round(frame[pixIndex + 1] * (1 - weight) + nextFrame[pixIndex + 1] * weight);
                            result[pixIndex + 2] = (byte)Math.Round(frame[pixIndex + 2] * (1 - weight) + nextFrame[pixIndex + 2] * weight);
                            pixIndex += PixelSize;
                        }
                    }
                }
            });

            var result = BitmapSource.Create(_width, _height, _dpiX, _dpiY, _pixelFormat, null, resultPixels, _width * PixelSize);
            result.Freeze();
            return result;
        }
    }
}
