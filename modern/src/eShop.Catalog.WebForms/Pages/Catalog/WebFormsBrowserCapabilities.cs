using System.Text.RegularExpressions;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>
/// Approximates the ASP.NET 4.x browser definition files used to pick uplevel validator rendering: the desktop/mobile
/// browsers they define (IE, Edge, Chrome, Firefox, Safari, Opera) are uplevel; unknown agents such as curl or
/// Python-urllib fall back to the Default definition (no ECMAScript) and get downlevel rendering.
/// </summary>
public static partial class WebFormsBrowserCapabilities
{
    public static bool IsUplevel(string? userAgent)
    {
        return !string.IsNullOrEmpty(userAgent) && UplevelAgent().IsMatch(userAgent);
    }

    [GeneratedRegex(@"^Mozilla/\d.*(MSIE|Trident/|Edge?/|Chrome/|Chromium/|Firefox/|Safari/|OPR/)|^Opera/")]
    private static partial Regex UplevelAgent();
}
