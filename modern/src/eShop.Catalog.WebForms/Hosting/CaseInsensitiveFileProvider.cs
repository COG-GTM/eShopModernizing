using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace eShop.Catalog.WebForms.Hosting;

/// <summary>
/// Owned by slice S6. IIS serves static files case-insensitively, so legacy pages and CSS mix casings
/// (<c>/images</c> vs <c>/Images</c>, <c>/Scripts/WebForms/MsAjax</c> vs the on-disk <c>MSAjax</c>,
/// <c>site.css</c> vs <c>Site.css</c>). wwwroot keeps one copy of each file; this provider resolves any casing to it.
/// Lookups go through the inner provider, so its root confinement (no <c>..</c>, no absolute paths) still applies.
/// </summary>
public sealed class CaseInsensitiveFileProvider : IFileProvider
{
    private readonly IFileProvider inner;

    public CaseInsensitiveFileProvider(IFileProvider inner)
    {
        this.inner = inner;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var exact = inner.GetFileInfo(subpath);
        if (exact.Exists)
        {
            return exact;
        }

        var resolved = Resolve(subpath);
        return resolved is null ? exact : inner.GetFileInfo(resolved);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var exact = inner.GetDirectoryContents(subpath);
        if (exact.Exists)
        {
            return exact;
        }

        var resolved = Resolve(subpath);
        return resolved is null ? exact : inner.GetDirectoryContents(resolved);
    }

    public IChangeToken Watch(string filter) => inner.Watch(filter);

    /// <summary>Returns the on-disk casing of <paramref name="subpath"/>, or null when no entry matches.</summary>
    public string? Resolve(string subpath)
    {
        var segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s == ".." || s == "." || s.Contains('\\')))
        {
            return null;
        }

        var current = string.Empty;
        foreach (var segment in segments)
        {
            var match = inner.GetDirectoryContents(current.Length == 0 ? "/" : current)
                .FirstOrDefault(e => string.Equals(e.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return null;
            }

            current += "/" + match.Name;
        }

        return current;
    }
}
