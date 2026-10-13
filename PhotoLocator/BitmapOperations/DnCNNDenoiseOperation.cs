#nullable disable

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
            if (source.PlaneCount != 1 && source.PlaneCount != 3)
                throw new NotSupportedException("DnCNNDenoiseOperation supports grayscale (1 channel) and RGB (3 channels) images only.");

            var channels = source.PlaneCount;
            var height = source.Height;
            var width = source.Width;

            var modelPath = ResolveModelPath(ModelPath);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"ONNX model not found: {modelPath}");

            var sourceCopy = new FloatBitmap(source);
            if (DstBitmap == null || DstBitmap == SrcBitmap)
                DstBitmap = new FloatBitmap(width, height, channels);
            else
                DstBitmap.New(width, height, channels);

            var inputBuffer = new float[1 * channels * height * width];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var index = (((0 * channels) + c) * height + y) * width + x;
                        inputBuffer[index] = sourceCopy.Elements[y, x * channels + c];
                    }
                }
            }

            using var session = new InferenceSession(modelPath);
            var inputName = session.InputMetadata.Keys.First();
            using var output = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<float>(inputBuffer, new[] { 1, channels, height, width })) });
            var outputTensor = output.First().AsTensor<float>();
            var outputBuffer = outputTensor.ToArray();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var c = 0; c < channels; c++)
                    {
                        var index = (((0 * channels) + c) * height + y) * width + x;
                        DstBitmap.Elements[y, x * channels + c] = Math.Clamp(outputBuffer[index], 0f, 1f);
                    }
                }
            }
        }
    }
}
