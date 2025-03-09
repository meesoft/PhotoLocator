using System;

namespace PhotoLocator.Helpers
{
    sealed class ActionDisposable : IDisposable
    {
        private readonly Action _callback;

        public ActionDisposable(Action callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            _callback();
        }
    }
}
