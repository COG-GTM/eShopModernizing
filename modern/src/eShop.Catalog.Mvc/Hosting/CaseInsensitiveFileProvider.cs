using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace eShop.Catalog.Mvc.Hosting;

/// <summary>
/// Resolves paths case-insensitively (IIS semantics) on case-sensitive file systems, so legacy URLs such as
/// /images/brand.png and /Images/brand.png, or /Content/site.css and /Content/Site.css, hit the single copy in wwwroot.
/// Exact-case hits go straight to the inner provider; only misses walk the directory listings.
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
        var file = inner.GetFileInfo(subpath);
        if (file.Exists)
        {
            return file;
        }

        var resolved = Resolve(subpath);
        return resolved is null ? file : inner.GetFileInfo(resolved);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = inner.GetDirectoryContents(subpath);
        if (contents.Exists)
        {
            return contents;
        }

        var resolved = Resolve(subpath);
        return resolved is null ? contents : inner.GetDirectoryContents(resolved);
    }

    public IChangeToken Watch(string filter)
    {
        return inner.Watch(filter);
    }

    private string? Resolve(string subpath)
    {
        var segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var current = string.Empty;
        foreach (var segment in segments)
        {
            var match = FindEntry(current, segment);
            if (match is null)
            {
                return null;
            }

            current = current + "/" + match;
        }

        return current;
    }

    private string? FindEntry(string directory, string name)
    {
        var contents = inner.GetDirectoryContents(directory.Length == 0 ? "/" : directory);
        if (!contents.Exists)
        {
            return null;
        }

        string? match = null;
        foreach (var entry in contents)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                return entry.Name;
            }

            if (match is null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                match = entry.Name;
            }
        }

        return match;
    }
}
