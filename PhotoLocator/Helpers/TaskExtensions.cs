using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    static class TaskExtensions
    {
        public static void WithExceptionShowing(this Task task)
        {
            task.ContinueWith(t => { ExceptionHandler.ShowException(t.Exception!); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void WithExceptionLogging(this Task task)
        {
            task.ContinueWith(t => { ExceptionHandler.LogException(t.Exception!); }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
