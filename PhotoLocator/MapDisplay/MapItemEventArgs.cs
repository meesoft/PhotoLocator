using MapControl;
using System;

namespace PhotoLocator.MapDisplay
{
    public class MapItemEventArgs : EventArgs
    {
        public MapItemEventArgs(object item)
        {
            Item = item;
        }

        public object Item { get; }
    }
}
