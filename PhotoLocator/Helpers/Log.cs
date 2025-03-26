using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PhotoLocator.Helpers
{
    static class Log
    {
        static readonly string[] _history = new string[100];
        static int _next;

        public static event Action<string>? EventAdded;

        public static void Write(string? message)
        {
            Debug.WriteLine(message);
            message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            _history[_next] = message;
            _next = (_next + 1) % _history.Length;
            EventAdded?.Invoke(message);
        }

        public static IEnumerable<string> GetHistory()
        {
            for (int i = 0; i < _history.Length; i++)
            {
                var index = (_next + i) % _history.Length;
                if (_history[index] is not null)
                    yield return _history[index];
            }
        }
    }
}
