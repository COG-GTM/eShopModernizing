using Microsoft.AspNetCore.Http;

namespace EShop.Storefront.WebForms.Hosting;

/// <summary>
/// Reproduces System.Web <c>Control.ResolveClientUrl("~/...")</c> for <c>runat="server"</c> links in Site.Master and the catalog
/// pages: the app-relative URL is rendered relative to the directory of the current request path, so on
/// <c>/Default/index/0/size/10</c> <c>~/favicon.ico</c> becomes <c>../../../../favicon.ico</c> and <c>~/</c> becomes <c>../../../../</c>
/// (<c>./</c> at the root). A fixed <c>favicon.ico</c> / <c>./</c> would resolve under the routed path and break on every nested route.
/// </summary>
public static class LegacyClientUrl
{
    public static string Resolve(HttpContext context, string appRelativeUrl)
    {
        var target = appRelativeUrl.StartsWith("~/") ? appRelativeUrl[2..] : appRelativeUrl.TrimStart('~');
        var path = (context.Request.PathBase + context.Request.Path).Value;
        if (string.IsNullOrEmpty(path)) path = "/";
        var depth = path[..(path.LastIndexOf('/') + 1)].Count(c => c == '/') - 1;
        if (depth <= 0) return target.Length == 0 ? "./" : target;
        return string.Concat(Enumerable.Repeat("../", depth)) + target;
    }
}
