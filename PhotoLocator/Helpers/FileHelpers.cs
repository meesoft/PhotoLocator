using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    static class FileHelpers
    {
        public static async Task<FileStream> OpenFileWithRetryAsync(string path, CancellationToken ct)
        {
            for (int i = 0; ; i++)
                try
                {
                    return File.OpenRead(path);
                }
                catch (IOException)
                {
                    if (i == 5)
                        throw;
                    await Task.Delay(1000, ct);
                }
        }
    }
}
