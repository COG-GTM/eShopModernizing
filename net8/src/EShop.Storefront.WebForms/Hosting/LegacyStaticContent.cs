using Microsoft.AspNetCore.StaticFiles;

namespace EShop.Storefront.WebForms.Hosting;

/// <summary>
/// Static files from wwwroot (case-insensitive) with the legacy media types (IIS/xsp serve .js as application/javascript) and the
/// System.Web.Optimization bundle URLs, which are extension-less files in wwwroot served with the bundle's media type.
/// </summary>
public static class LegacyStaticContent
{
    public static StaticFileOptions CreateOptions(string webRootPath, IReadOnlyDictionary<string, string> bundles) => new()
    {
        FileProvider = new CaseInsensitiveFileProvider(webRootPath),
        ContentTypeProvider = new LegacyContentTypeProvider(bundles),
    };

    private sealed class LegacyContentTypeProvider : IContentTypeProvider
    {
        private readonly FileExtensionContentTypeProvider _inner = new();
        private readonly IReadOnlyDictionary<string, string> _bundles;

        public LegacyContentTypeProvider(IReadOnlyDictionary<string, string> bundles)
        {
            _bundles = bundles;
            _inner.Mappings[".js"] = "application/javascript";
        }

        public bool TryGetContentType(string subpath, out string contentType)
        {
            if (_bundles.TryGetValue(subpath, out var bundleType))
            {
                contentType = bundleType;
                return true;
            }
            return _inner.TryGetContentType(subpath, out contentType!);
        }
    }
}
