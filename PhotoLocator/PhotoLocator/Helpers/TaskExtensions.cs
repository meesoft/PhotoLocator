using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    static class TaskExtensions
    {
        public static void WithExceptionLogging(this Task task)
        {
            task.ContinueWith(t => { Debug.WriteLine(t.Exception?.ToString()); }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
