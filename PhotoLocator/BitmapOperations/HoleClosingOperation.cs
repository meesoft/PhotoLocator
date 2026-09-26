using PhotoLocator.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator.BitmapOperations;

static class HoleClosingOperation
{
    public readonly struct HolePatch
    {
        public int Target { get; init; }
        public int Source { get; init; }
    }

    public static List<HolePatch> FindHolePatches(byte[] mask, int width, int height, int planeCount, int holeThreshold, CancellationToken ct)
    {
        var holePatches = new List<HolePatch>();
        int stride = width * planeCount;
        Parallel.For(0, height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            var rowStart = y * stride;
            for (int x = 0; x < stride; x++)
                if (mask[rowStart + x] > holeThreshold)
                {
                    bool found = false;
                    for (int radius = 1; !found && radius < 100; radius++)
                        for (int d = 0; !found && d < radius; d++)
                            // ^ <1 5>
                            // 7     ^
                            // 3  *  4
                            // ·     8
                            // <6 2> ·
                            found =
                                CheckCandidate(x - d * planeCount, y - radius) ||       // 1
                                CheckCandidate(x + d * planeCount, y + radius) ||       // 2
                                CheckCandidate(x - radius * planeCount, y + d) ||       // 3
                                CheckCandidate(x + radius * planeCount, y - d) ||       // 4
                                CheckCandidate(x + (d + 1) * planeCount, y - radius) || // 5
                                CheckCandidate(x - (d + 1) * planeCount, y + radius) || // 6
                                CheckCandidate(x - radius * planeCount, y - (d + 1)) || // 7
                                CheckCandidate(x + radius * planeCount, y + d + 1);     // 8
                    if (!found)
                        throw new UserMessageException("Bad dark frame, unable to patch hot pixel");

                    bool CheckCandidate(int sx, int sy)
                    {
                        if (sx < 0 || sy < 0 || sx >= stride || sy >= height || mask[sy * stride + sx] > holeThreshold)
                            return false;
                        lock (holePatches)
                            holePatches.Add(new HolePatch
                            {
                                Target = rowStart + x,
                                Source = sy * stride + sx,
                            });
                        return true;
                    }
                }
        });
        return holePatches;
    }

    /// <summary>
    /// Fill holes in image where the corresponding pixel in the grayscale mask is above threshold
    /// </summary>
    /// <param name="image">Color or grayscale image</param>
    /// <param name="mask">Grayscale mask</param>
    /// <param name="threshold">Threshold in mask</param>
    public static void CloseHolesIteratively(FloatBitmap image, FloatBitmap mask, float threshold)
    {
        if (image.Width != mask.Width || image.Height != mask.Height)
            throw new ArgumentException("Image and mask dimensions must match.");

        var width = image.Width;
        var height = image.Height;
        var planeCount = image.PlaneCount;
        var holes = new bool[width * height];
        var remaining = 0;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (mask[x, y] > threshold)
                {
                    holes[y * width + x] = true;
                    remaining++;
                }
        Log.Write($"Detected {remaining * 100 / holes.Length}% holes in mask");

        while (remaining > 0)
        {
            var replacements = new List<(int X, int Y, float[] Values)>();

            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (!holes[y * width + x])
                        continue;

                    var sums = new float[planeCount];
                    var count = 0;
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            var nx = x + dx;
                            var ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height || holes[ny * width + nx])
                                continue;

                            for (var p = 0; p < planeCount; p++)
                                sums[p] += image.Elements[ny, nx * planeCount + p];
                            count++;
                        }

                    if (count > 0)
                    {
                        for (var p = 0; p < planeCount; p++)
                            sums[p] /= count;
                        replacements.Add((x, y, sums));
                    }
                }

            if (replacements.Count == 0)
                break;

            foreach (var replacement in replacements)
            {
                var offset = replacement.X * planeCount;
                for (var p = 0; p < planeCount; p++)
                    image.Elements[replacement.Y, offset + p] = replacement.Values[p];
                holes[replacement.Y * width + replacement.X] = false;
            }
            remaining -= replacements.Count;
        }
    }
}
