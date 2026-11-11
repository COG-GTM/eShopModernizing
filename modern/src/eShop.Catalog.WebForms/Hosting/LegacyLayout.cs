namespace eShop.Catalog.WebForms.Hosting;

/// <summary>Owned by slice S6. Helpers that reproduce what legacy Site.Master rendered for a given request URL.</summary>
public static class LegacyLayout
{
    /// <summary>
    /// ViewData key: set to <c>true</c> to render the legacy page-wide <c>&lt;form id="ctl00_ctl01"&gt;</c> (with the
    /// antiforgery token in <c>div.aspNetHidden</c>) around the page. Off by default so pages can own their forms.
    /// </summary>
    public const string PageFormKey = "LegacyPageForm";

    public const string TitleSuffix = " - Catalog manager (Web Forms)";

    /// <summary>
    /// WebForms resolves <c>~/</c> relative to the request directory: "" at the root level, "../" per extra level
    /// (/Catalog/Details/1 -> "../../").
    /// </summary>
    public static string RelativeRoot(PathString path)
    {
        var value = path.HasValue ? path.Value! : "/";
        var depth = value.Count(c => c == '/') - 1;
        return string.Concat(Enumerable.Repeat("../", Math.Max(depth, 0)));
    }

    /// <summary>Header brand link: <c>href="~/"</c> resolved (<c>./</c> at the root level).</summary>
    public static string HomeHref(PathString path)
    {
        var root = RelativeRoot(path);
        return root.Length == 0 ? "./" : root;
    }

    /// <summary>
    /// <c>HtmlForm</c> posts back to the last URL segment plus the query string; the site root is served by the
    /// default document, so it posts to <c>Default.aspx</c>.
    /// </summary>
    public static string FormAction(PathString path, QueryString query)
    {
        var value = path.HasValue ? path.Value! : "/";
        var last = value[(value.LastIndexOf('/') + 1)..];
        return (last.Length == 0 ? "Default.aspx" : last) + query.Value;
    }
}
