using System;
using System.Windows.Input;

namespace PhotoLocator.Helpers
{
    public class CursorOverride : IDisposable
    {
        private Cursor _previousCursor;

        /// <summary>
        /// Set cursor, null for default wait cursor
        /// </summary>
        public CursorOverride(Cursor? cursor = null)
        {
            _previousCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = cursor ?? Cursors.Wait;
        }

        public void Dispose()
        {
            Mouse.OverrideCursor = _previousCursor;
        }
    }
}
