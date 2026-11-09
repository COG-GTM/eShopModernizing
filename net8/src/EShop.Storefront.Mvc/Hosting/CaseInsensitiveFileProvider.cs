using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// wwwroot lookups that ignore case, like IIS (and xsp with MONO_IOMAP=all): the legacy markup references both
/// images/ and Images/, Content/site.css and Content/Site.css. Exact-case hits go straight to the physical provider;
/// a miss is resolved segment by segment against the real directory entries, so it cannot leave the root.
/// </summary>
public sealed class CaseInsensitiveFileProvider : IFileProvider
{
    private readonly string _root;
    private readonly PhysicalFileProvider _inner;

    public CaseInsensitiveFileProvider(string root)
    {
        _root = Path.GetFullPath(root);
        _inner = new PhysicalFileProvider(_root);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var file = _inner.GetFileInfo(subpath);
        if (file.Exists)
        {
            return file;
        }
        var resolved = Resolve(subpath, expectDirectory: false);
        return resolved is null ? file : _inner.GetFileInfo(resolved);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = _inner.GetDirectoryContents(subpath);
        if (contents.Exists)
        {
            return contents;
        }
        var resolved = Resolve(subpath, expectDirectory: true);
        return resolved is null ? contents : _inner.GetDirectoryContents(resolved);
    }

    public IChangeToken Watch(string filter) => _inner.Watch(filter);

    private string? Resolve(string subpath, bool expectDirectory)
    {
        var segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }
        var current = _root;
        for (var i = 0; i < segments.Length; i++)
        {
            var last = i == segments.Length - 1;
            var match = FindEntry(current, segments[i], findDirectory: !last || expectDirectory);
            if (match is null)
            {
                return null;
            }
            current = match;
        }
        return "/" + Path.GetRelativePath(_root, current).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string? FindEntry(string directory, string name, bool findDirectory)
    {
        try
        {
            var entries = findDirectory ? Directory.EnumerateDirectories(directory) : Directory.EnumerateFiles(directory);
            return entries.FirstOrDefault(e => string.Equals(Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
