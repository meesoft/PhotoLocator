using System.IO;
using System.Linq;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class DnCNNDenoiseOperationTest
    {
        private static string GetModelPath()
        {
            var root = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(root, "TestData", "dncnn_test.onnx"),
                Path.Combine(root, "..", "..", "..", "TestData", "dncnn_test.onnx"),
                Path.Combine(root, "..", "..", "..", "..", "TestData", "dncnn_test.onnx"),
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return Path.GetFullPath(Path.Combine(root, "TestData", "dncnn_test.onnx"));
        }

        [TestMethod]
        public void Apply_ShouldRunModel_WhenPretrainedOnnxExists()
        {
            var modelPath = GetModelPath();
            if (!File.Exists(modelPath))
                Assert.Inconclusive($"Pretrained ONNX file not found: {modelPath}. Add a trained dncnn model to PhotoLocatorTest/TestData/dncnn_test.onnx to run this test.");

            var bitmap = new FloatBitmap(8, 8, 3);
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    bitmap.Elements[y, x * 3 + 0] = ((x * 13 + y * 7) % 17) / 16f;
                    bitmap.Elements[y, x * 3 + 1] = (float)(0.5 + 0.2 * Math.Sin(x * 0.7 + y * 0.9));
                    bitmap.Elements[y, x * 3 + 2] = (float)(1.0 - 0.3 * Math.Cos(x * 0.5 + y * 0.4));
                }
            }

            var op = new DnCNNDenoiseOperation
            {
                SrcBitmap = bitmap,
                ModelPath = modelPath,
            };

            op.Apply();

            Assert.IsNotNull(op.DstBitmap);
            Assert.AreEqual(bitmap.Width, op.DstBitmap.Width);
            Assert.AreEqual(bitmap.Height, op.DstBitmap.Height);
            Assert.AreEqual(bitmap.PlaneCount, op.DstBitmap.PlaneCount);
            Assert.IsTrue(op.DstBitmap.Elements.Cast<float>().All(v => !float.IsNaN(v)), "Model output contains NaN values.");
            Assert.IsTrue(op.DstBitmap.Elements.Cast<float>().All(v => v >= 0f && v <= 1f), "Model output is outside the normalized [0,1] range.");
        }
    }
}
