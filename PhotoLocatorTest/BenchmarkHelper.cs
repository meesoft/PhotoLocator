using System.Diagnostics;
using System.Linq;

namespace PhotoLocator
{
    public class BenchmarkHelper
    {
        public static void Run(Action action, int innerLoops = 1, int outerIterations = 5)
        {
#if DEBUG
            Console.WriteLine("WARNING: Running benchmark in DEBUG mode. Results may not reflect release performance.");
#endif
            var iterationTimes = new long[outerIterations];
            for (int i = 0; i < outerIterations; i++)
            {
                GC.Collect();
                GC.TryStartNoGCRegion(1024 * 1024 * 100);
                var sw = Stopwatch.StartNew();
                for (int j = 0; j < innerLoops; j++)
                    action();
                sw.Stop();
                Console.WriteLine($"Iteration {i + 1}: {sw.ElapsedMilliseconds} ms");
                GC.EndNoGCRegion();
                iterationTimes[i] = sw.ElapsedMilliseconds;
            }
            var median = iterationTimes.Order().Skip(outerIterations / 2).First();
            var min = iterationTimes.Min();
            Console.WriteLine($"Median time: {median} ms");
            Console.WriteLine($"Minimum time: {min} ms");
            throw new AssertFailedException($"Min={min} ms, median={median} ms");
        }
    }
}
