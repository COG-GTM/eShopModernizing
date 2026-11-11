namespace eShop.Catalog.WebForms.Pages;

/// <summary>Helpers that reproduce how the legacy WebForms pages render URLs.</summary>
public static class LegacyPage
{
    /// <summary>ViewData key that makes the S6 layout render the legacy page-wide post form (Site.Master parity).</summary>
    public const string PageFormKey = "LegacyPageForm";

    /// <summary>
    /// WebForms resolves <c>href="~"</c> on a runat=server anchor relative to the request directory,
    /// e.g. "../../" on /Catalog/Details/1.
    /// </summary>
    public static string AppRootRelativeTo(string? requestPath)
    {
        var path = string.IsNullOrEmpty(requestPath) ? "/" : requestPath;
        var depth = path.Count(c => c == '/') - 1;
        return depth <= 0 ? "./" : string.Concat(Enumerable.Repeat("../", depth));
    }
}
