using System;

namespace PhotoLocator.Extensions;

public static class StringExtension
{
    /// <summary>
    /// Resolve invalid number format ".-123" which should be "-0.123". Occurs i.e. with gotoextr in the region of London.
    /// </summary>
    /// <param name="floatText">Text to be checked for invalid negative number definition</param>
    /// <returns>Input text cleaned of invalid format e.g. ".-123" -> "-0.123"</returns>
    public static string ResolveInvalidNumberFormat(this string floatText)
    {
        if (floatText.StartsWith(".-", StringComparison.InvariantCultureIgnoreCase))
            floatText = floatText.Replace(".-", "-0.", StringComparison.InvariantCultureIgnoreCase);

        return floatText;
    }
}
