using System;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    sealed class ActionDisposable : IAsyncDisposable
    {
        private readonly Func<ValueTask> _callback;

        public ActionDisposable(Func<ValueTask> callback)
        {
            _callback = callback;
        }

        public ValueTask DisposeAsync()
        {
            return _callback();
        }
    }
}
