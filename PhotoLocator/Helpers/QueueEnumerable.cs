using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    sealed class QueueEnumerable<T> : IEnumerable<T>, IEnumerator<T>
    {
        readonly AutoResetEvent _nextSet = new(false);
        readonly AutoResetEvent _nextTaken = new(true);
        readonly TaskCompletionSource _gotFirst = new();
        T _next = default!;
        bool _break;

        public void AddItem(T item)
        {
            if (_break)
                return;
            _nextTaken.WaitOne();
            _next = item;
            _nextSet.Set();
            _gotFirst.TrySetResult();
        }

        public void Break()
        {
            if (_break)
                return;
            _nextTaken.WaitOne();
            _break = true;
            _nextSet.Set();
        }

        public Task GotFirst => _gotFirst.Task;

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
            if (!_break)
            {
                _break = true;
                _nextSet.Set();
                _nextTaken.Set();
            }
            _nextSet.Dispose();
            _nextTaken.Dispose();
        }
    }
}
