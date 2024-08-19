using System;
using System.Windows.Input;

namespace PhotoLocator.Helpers
{
    public sealed class MouseCursorOverride : IDisposable
    {
        private Cursor _previousCursor;

        /// <summary>
        /// Set cursor, null for default wait cursor
        /// </summary>
        public MouseCursorOverride(Cursor? cursor = null)
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
