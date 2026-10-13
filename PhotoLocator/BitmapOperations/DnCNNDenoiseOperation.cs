using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.IO;
using System.Linq;

namespace PhotoLocator.BitmapOperations
{
    public class DnCNNDenoiseOperation : OperationBase
    {
        public const string DefaultModelFileName = "dncnn.onnx";

        public int TileSize { get; set; } = 512;

        public int TileOverlap { get; set; } = 32;

        public string ModelPath { get; set; } = DefaultModelFileName;

        public static string ResolveModelPath(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                modelPath = DefaultModelFileName;
            if (!Path.IsPathRooted(modelPath))
                modelPath = Path.Combine(AppContext.BaseDirectory, modelPath);
            return modelPath;
        }

        public static string? TryFindModelInAppDirectory()
        {
            foreach (var candidate in new[]
            {
                DefaultModelFileName,
                Path.Combine("models", DefaultModelFileName),
                Path.Combine("ai", DefaultModelFileName)
            })
            {
                var fullPath = ResolveModelPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        public override void Apply()
        {
            if (string.IsNullOrWhiteSpace(ModelPath))
                throw new InvalidOperationException("ModelPath must be set before applying DnCNNDenoiseOperation.");
            if (SrcBitmap == null)
                throw new InvalidOperationException("SrcBitmap cannot be null.");

            var source = SrcBitmap;
            if (source.PlaneCount is not 1 and not 3)
                throw new NotSupportedException("DnCNNDenoiseOperation supports grayscale (1 channel) and RGB (3 channels) images only.");

            var channels = source.PlaneCount;
            var height = source.Height;
            var width = source.Width;

            var modelPath = ResolveModelPath(ModelPath);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"ONNX model not found: {modelPath}");

            if (TileSize <= 0)
                TileSize = 512;
            if (TileOverlap < 0)
                TileOverlap = 0;
            if (TileOverlap >= TileSize)
                TileOverlap = Math.Max(0, TileSize / 4);

            var sourceCopy = new FloatBitmap(source);
            if (DstBitmap == null || DstBitmap == SrcBitmap)
                DstBitmap = new FloatBitmap(width, height, channels);
            else
                DstBitmap.New(width, height, channels);

            if (width <= TileSize && height <= TileSize)
            {
                RunTileInference(modelPath, sourceCopy, 0, 0, width, height, channels, DstBitmap, 0, 0);
                return;
            }

            var accumulator = new float[height, width, channels];
            var weightMap = new float[height, width, channels];
            var step = Math.Max(1, TileSize - TileOverlap);

            for (var top = 0; top < height; top += step)
            {
                var tileTop = top;
                var tileBottom = Math.Min(top + TileSize, height);
                for (var left = 0; left < width; left += step)
                {
                    var tileLeft = left;
                    var tileRight = Math.Min(left + TileSize, width);
                    var tileWidth = tileRight - tileLeft;
                    var tileHeight = tileBottom - tileTop;

                    var tileDst = new FloatBitmap(tileWidth, tileHeight, channels);
                    RunTileInference(modelPath, sourceCopy, tileLeft, tileTop, tileWidth, tileHeight, channels, tileDst, 0, 0);

                    for (var y = 0; y < tileHeight; y++)
                    {
                        var globalY = tileTop + y;
                        for (var x = 0; x < tileWidth; x++)
                        {
                            var globalX = tileLeft + x;
                            for (var c = 0; c < channels; c++)
                            {
                                accumulator[globalY, globalX, c] += tileDst.Elements[y, x * channels + c];
                                weightMap[globalY, globalX, c] += 1f;
                            }
                        }
                    }
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var value = weightMap[y, x, c] > 0 ? accumulator[y, x, c] / weightMap[y, x, c] : sourceCopy.Elements[y, x * channels + c];
                        DstBitmap.Elements[y, x * channels + c] = Math.Clamp(value, 0f, 1f);
                    }
                }
            }
        }

        private static void RunTileInference(string modelPath, FloatBitmap source, int srcLeft, int srcTop, int tileWidth, int tileHeight, int channels, FloatBitmap dstBitmap, int dstLeft, int dstTop)
        {
            var inputBuffer = new float[1 * channels * tileHeight * tileWidth];
            for (var y = 0; y < tileHeight; y++)
            {
                var sourceY = srcTop + y;
                for (var x = 0; x < tileWidth; x++)
                {
                    var sourceX = srcLeft + x;
                    for (var c = 0; c < channels; c++)
                    {
                        var index = (((0 * channels) + c) * tileHeight + y) * tileWidth + x;
                        inputBuffer[index] = source.Elements[sourceY, sourceX * channels + c];
                    }
                }
            }

            using var session = new InferenceSession(modelPath);
            var inputName = session.InputMetadata.Keys.First();
            using var output = session.Run([NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(inputBuffer, [1, channels, tileHeight, tileWidth]))]);
            var outputBuffer = output[0].AsTensor<float>().ToArray();

            for (var y = 0; y < tileHeight; y++)
            {
                for (var x = 0; x < tileWidth; x++)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var index = (((0 * channels) + c) * tileHeight + y) * tileWidth + x;
                        var outValue = Math.Clamp(outputBuffer[index], 0f, 1f);
                        var dstY = dstTop + y;
                        var dstX = dstLeft + x;
                        if (dstY >= 0 && dstY < dstBitmap.Height && dstX >= 0 && dstX < dstBitmap.Width)
                            dstBitmap.Elements[dstY, dstX * channels + c] = outValue;
                    }
                }
            }
        }
    }
}
