using System.Text;
using Microsoft.Extensions.FileProviders;

namespace eShopNet8.Mvc.Extensions;

// System.Web.Optimization bundle URLs from the legacy BundleConfig. Pages reference the individual files (debug
// rendering, as recorded in the baseline); the bundle URLs still answer with the concatenated, unminified files.
public static class LegacyBundles
{
    private static readonly (string Url, string ContentType, string[] Files)[] Bundles =
    {
        ("/bundles/jquery", "text/javascript", new[] { "Scripts/jquery-3.3.1.js" }),
        ("/bundles/jqueryval", "text/javascript", new[] { "Scripts/jquery.validate.js", "Scripts/jquery.validate.unobtrusive.js" }),
        ("/bundles/modernizr", "text/javascript", new[] { "Scripts/modernizr-2.6.2.js", "Scripts/modernizr-2.8.3.js" }),
        ("/bundles/bootstrap", "text/javascript", new[] { "Scripts/bootstrap.js", "Scripts/respond.js" }),
        ("/Content/css", "text/css", new[] { "Content/bootstrap.css", "Content/custom.css", "Content/base.css", "Content/Site.css" }),
    };

    public static void MapLegacyBundles(this WebApplication app)
    {
        foreach (var (url, contentType, files) in Bundles)
        {
            app.MapGet(url, async (IWebHostEnvironment env) =>
            {
                var sb = new StringBuilder();
                foreach (var file in files)
                {
                    var info = env.WebRootFileProvider.GetFileInfo(file);
                    if (!info.Exists) continue;
                    using var reader = new StreamReader(info.CreateReadStream());
                    sb.Append(await reader.ReadToEndAsync()).Append('\n');
                }
                return Results.Text(sb.ToString(), contentType + "; charset=utf-8");
            });
        }
    }
}
