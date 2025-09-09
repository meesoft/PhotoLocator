using System;

namespace PhotoLocator.MapDisplay
{
    public class MapItemEventArgs : EventArgs
    {
        public MapItemEventArgs(PointItem item)
        {
            Item = item;
        }

        public PointItem Item { get; }
    }
}
