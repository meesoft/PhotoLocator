using System.Diagnostics;

namespace PhotoLocator.Helpers
{
    [TestClass]
    public class LanczosResizeOperationTest
    {
        [TestMethod]
        public void Apply_ShouldReduce()
        {
            const int SrcSize = 40;
            const int DstSize = 30;

            var op = new LanczosResizeOperation
            {
                FilterFunc = LanczosResizeOperation.Lanczos3,
                FilterWindow = 3,
            };

            var srcPixels = new byte[SrcSize * SrcSize * 3];
            for (var i = 0; i < srcPixels.Length; i++)
                srcPixels[i] = 10;

            //op.Apply(srcPixels, SrcSize, SrcSize, 3, 3, DstSize, DstSize, default);

            var sw = Stopwatch.StartNew();
            var dstPixels = op.Apply(srcPixels, SrcSize, SrcSize, 3, 3, DstSize, DstSize, default);
            Console.WriteLine(sw.ElapsedMilliseconds + "ms");

            Assert.AreEqual(dstPixels.Length * 10, dstPixels.Sum(b => b));
        }
    }
}
