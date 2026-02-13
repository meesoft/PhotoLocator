namespace PhotoLocator.Helpers
{
    static class StringExtensions
    {
        public static string TrimPath(this string path)
        {
            return path.Trim(' ', '"');
        }

        public static string TrimInvariantValue(this string str)
        {
            return str.Trim().Replace(',', '.');
        }
    }
}
