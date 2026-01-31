using System;

namespace PhotoLocator.MapDisplay
{
    public class MapItemEventArgs(IPointItem item) : EventArgs
    {
        public IPointItem Item { get; } = item;
    }
}
