using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoLocator.Helpers
{
    class SelectorComparer<T> : IComparer<T>
    {
        readonly Func<T, IComparable> _selector;

        public SelectorComparer(Func<T, IComparable> selector)
        {
            _selector = selector;
        }

        public int Compare(T? x, T? y)
        {
            if (x == null || y == null)
                return 0;
            return _selector(x).CompareTo(_selector(y));
        }
    }
}
