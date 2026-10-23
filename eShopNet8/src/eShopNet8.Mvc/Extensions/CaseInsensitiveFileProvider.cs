using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace eShopNet8.Mvc.Extensions;

// Resolves paths segment by segment ignoring case when the exact path does not exist (IIS semantics on Linux).
public sealed class CaseInsensitiveFileProvider : IFileProvider
{
    private readonly IFileProvider _inner;

    public CaseInsensitiveFileProvider(IFileProvider inner) => _inner = inner;

    public IFileInfo GetFileInfo(string subpath)
    {
        var info = _inner.GetFileInfo(subpath);
        if (info.Exists) return info;
        var resolved = Resolve(subpath);
        return resolved is null ? info : _inner.GetFileInfo(resolved);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = _inner.GetDirectoryContents(subpath);
        if (contents.Exists) return contents;
        var resolved = Resolve(subpath);
        return resolved is null ? contents : _inner.GetDirectoryContents(resolved);
    }

    public IChangeToken Watch(string filter) => _inner.Watch(filter);

    // Only reached for paths that do not exist as written; deliberately uncached so request paths cannot grow memory.
    private string? Resolve(string subpath)
    {
        var current = "";
        foreach (var segment in subpath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..") return null;
            var match = _inner.GetDirectoryContents(current)
                .FirstOrDefault(e => string.Equals(e.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            current = current + "/" + match.Name;
        }
        return current.Length == 0 ? null : current;
    }
}
