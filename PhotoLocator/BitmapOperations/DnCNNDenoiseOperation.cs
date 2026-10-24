using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhotoLocator.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations;

public class DnCNNDenoiseOperation : OperationBase
{
    public const string DefaultModelFileName = "dncnn.onnx";

    public int TileSize { get; set; } = 512;

    public int TileOverlap { get; set; } = 32;

    public string ModelPath { get; set; } = DefaultModelFileName;

    public bool PreferDirectML { get; set; } = true;

    public bool ExpectColorModel { get; set; }

    public CancellationToken CancellationToken { get; set; }

    public Action<double>? ProgressCallback { get; set; }

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
        if (DstBitmap == null)
            throw new InvalidOperationException("DstBitmap cannot be null.");

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

        bool processGrayscaleWithColorModel = ExpectColorModel && channels == 1;
        if (processGrayscaleWithColorModel)
        {
            source = new FloatBitmap(SrcBitmap.Width, SrcBitmap.Height, 3);
            source.ProcessElementWise(SrcBitmap, (_, s) => s);
            channels = 3;
        }

        using var session = CreateSession(modelPath, PreferDirectML);

        if (width <= TileSize && height <= TileSize)
        {
            DstBitmap.New(width, height, channels);
            RunTileInference(session, source, 0, 0, width, height, channels, DstBitmap, 0, 0);
            return;
        }

        var accumulator = new float[height, width, channels];
        var weightMap = new float[height, width];
        var step = Math.Max(1, TileSize - TileOverlap);

        for (var top = 0; top < height; top += step)
        {
            var tileTop = top;
            var tileBottom = Math.Min(top + TileSize, height);
            var tileHeight = tileBottom - tileTop;
            for (var left = 0; left < width; left += step)
            {
                CancellationToken.ThrowIfCancellationRequested();
                var tileLeft = left;
                var tileRight = Math.Min(left + TileSize, width);
                var tileWidth = tileRight - tileLeft;

                var tileDst = new FloatBitmap(tileWidth, tileHeight, channels);
                RunTileInference(session, source, tileLeft, tileTop, tileWidth, tileHeight, channels, tileDst, 0, 0);
				var topOverlap = tileTop > 0 ? Math.Min(TileOverlap, tileHeight) : 0;
				var bottomOverlap = tileBottom < height ? Math.Min(TileOverlap, tileHeight) : 0;
				var leftOverlap = tileLeft > 0 ? Math.Min(TileOverlap, tileWidth) : 0;
				var rightOverlap = tileRight < width ? Math.Min(TileOverlap, tileWidth) : 0;

                Parallel.For(0, tileHeight, y =>
                {
                    var globalY = tileTop + y;
                    var yWeight = GetRampWeight(y, tileHeight, topOverlap, bottomOverlap);
                    for (var x = 0; x < tileWidth; x++)
                    {
                        var globalX = tileLeft + x;
                        var weight = yWeight * GetRampWeight(x, tileWidth, leftOverlap, rightOverlap);
                        weightMap[globalY, globalX] += weight;
                        for (var c = 0; c < channels; c++)
                            accumulator[globalY, globalX, c] += tileDst.Elements[y, x * channels + c] * weight;
                    }
                });
                ProgressCallback?.Invoke((top + (left + tileWidth) / (double)width * tileHeight) / height);
            }
        }

        CancellationToken.ThrowIfCancellationRequested();
        DstBitmap.New(width, height, channels);
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                if (weightMap[y, x] == 0)
                    throw new InvalidOperationException($"Weight map at ({x}, {y}) is zero, which should not happen.");
                for (var c = 0; c < channels; c++)
                    DstBitmap.Elements[y, x * channels + c] = RealMath.Clamp(accumulator[y, x, c] / weightMap[y, x], 0, 1);
            }
        });

        if (processGrayscaleWithColorModel)
            DstBitmap.Assign(ConvertToGrayscaleOperation.ConvertToGrayscale(DstBitmap));
    }

    private static float GetRampWeight(int position, int length, int leadingOverlap, int trailingOverlap)
    {
        var weight = 1f;
        if (leadingOverlap > 0 && position < leadingOverlap)
            weight = (position + 1) / (leadingOverlap + 1);
        if (trailingOverlap > 0 && position >= length - trailingOverlap)
            weight = Math.Min(weight, (length - position) / (float)(trailingOverlap + 1));
        return weight;
    }

    private static InferenceSession CreateSession(string modelPath, bool preferDirectML)
    {
        if (!preferDirectML)
            return new InferenceSession(modelPath);

        try
        {
            using var sessionOptions = new SessionOptions();
            sessionOptions.AppendExecutionProvider_DML(0);
            return new InferenceSession(modelPath, sessionOptions);
        }
        catch (Exception exception) when (exception is OnnxRuntimeException or DllNotFoundException)
        {
            return new InferenceSession(modelPath);
        }
    }

    private static void RunTileInference(InferenceSession session, FloatBitmap source, int srcLeft, int srcTop, int tileWidth, int tileHeight, int channels, FloatBitmap dstBitmap, int dstLeft, int dstTop)
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
                    var index = (c * tileHeight + y) * tileWidth + x;
                    inputBuffer[index] = source.Elements[sourceY, sourceX * channels + c];
                }
            }
        }

        var inputName = session.InputMetadata.Keys.First();
        using var output = session.Run([NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(inputBuffer, [1, channels, tileHeight, tileWidth]))]);
        var outputBuffer = output[0].AsTensor<float>().ToArray();

        for (var y = 0; y < tileHeight; y++)
        {
            for (var x = 0; x < tileWidth; x++)
            {
                for (var c = 0; c < channels; c++)
                {
                    var index = (c * tileHeight + y) * tileWidth + x;
                    var outValue = RealMath.Clamp(outputBuffer[index], 0, 1);
                    var dstY = dstTop + y;
                    var dstX = dstLeft + x;
                    if (dstY >= 0 && dstY < dstBitmap.Height && dstX >= 0 && dstX < dstBitmap.Width)
                        dstBitmap.Elements[dstY, dstX * channels + c] = outValue;
                }
            }
        }
    }
}
