using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace PhotoLocator.Helpers
{
    sealed class CallbackEnumerable<T> : IEnumerable<T>, IEnumerator<T>
    {
        AutoResetEvent _nextSet = new(false);
        AutoResetEvent _nextTaken = new(true);
        T _next = default!;
        bool _break;

        public void ItemCallback(T item)
        {
            _nextTaken.WaitOne();
            _next = item;
            _nextSet.Set();
        }

        public void Break()
        {
            if (_break)
                return;
            _nextTaken.WaitOne();
            _break = true;
            _nextSet.Set();
        }

        public IEnumerator<T> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;

        public void Reset()
        {
            if (_break)
                throw new InvalidOperationException("Can only be enumerated once");
        }

        public T Current { get; private set; } = default!;

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _nextSet.WaitOne();
            if (_break)
                return false;
            Current = _next;
            _nextTaken.Set();
            return true;
        }

        public void Dispose()
        {
            _break = true;
            _nextSet.Dispose();
            _nextTaken.Dispose();
        }
    }
}
