using System.Globalization;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Converts a route/query value to <c>int</c> the way Web API 2 model binding did (System.ComponentModel.Int32Converter):
/// surrounding white space and a leading sign are allowed, <c>0x</c>/<c>&amp;h</c>/<c>#</c> prefixes mean hexadecimal,
/// anything else (decimals, overflow, empty) fails.
/// </summary>
public static class LegacyInt32Parser
{
    public static bool TryParse(string? text, out int value)
    {
        value = 0;
        if (text is null)
        {
            return false;
        }

        var s = text.Trim();
        string? hex = null;
        if (s.StartsWith('#'))
        {
            hex = s[1..];
        }
        else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("&h", StringComparison.OrdinalIgnoreCase))
        {
            hex = s[2..];
        }

        return hex is null
            ? int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            : hex.Length > 0 && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }
}
