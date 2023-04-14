using MapControl;
using System;

namespace PhotoLocator.MapDisplay
{
    public class MapItemEventArgs : EventArgs
    {
        public MapItemEventArgs(MapItem item)
        {
            Item = item;
        }

        public MapItem Item { get; }
    }
}
