namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// Bundle URLs registered by the legacy App_Start/BundleConfig.cs. Each URL is a file of the same path in wwwroot holding the
/// legacy System.Web.Optimization response body (WebGrease-minified scripts; the style bundle is served unminified behind the
/// "Minification failed" header because WebGrease cannot parse Bootstrap 4 custom properties). Regenerate a bundle with
/// `curl -o wwwroot/bundles/jquery http://127.0.0.1:5100/bundles/jquery` against the legacy harness.
/// </summary>
public static class LegacyBundles
{
    private const string Script = "text/javascript";
    private const string Style = "text/css";

    public static readonly IReadOnlyDictionary<string, string> Mvc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["/bundles/jquery"] = Script,      // ~/Scripts/jquery-{version}.js
        ["/bundles/jqueryval"] = Script,   // ~/Scripts/jquery.validate*
        ["/bundles/modernizr"] = Script,   // ~/Scripts/modernizr-*
        ["/bundles/bootstrap"] = Script,   // ~/Scripts/bootstrap.js, ~/Scripts/respond.js
        ["/Content/css"] = Style,          // bootstrap.css, custom.css, base.css, site.css
    };
}
