namespace eShopNet8.WebForms.Extensions;

// Helpers reproducing how WebForms rendered URLs relative to the current request.
public static class LegacyPage
{
    public const string SessionMachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    // "~/" resolved relative to the request path: "./" at the root, "../" per extra path segment.
    public static string AppRelativeRoot(HttpRequest request)
    {
        var depth = Depth(request);
        return depth == 0 ? "./" : string.Concat(Enumerable.Repeat("../", depth));
    }

    // Relative form of a root file ("favicon.ico", "../favicon.ico", ...).
    public static string AppRelative(HttpRequest request, string file)
    {
        var depth = Depth(request);
        return depth == 0 ? file : string.Concat(Enumerable.Repeat("../", depth)) + file;
    }

    // HtmlForm action: the last segment of the requested URL ("/" was served by Default.aspx).
    public static string FormAction(HttpRequest request)
    {
        var path = request.Path.Value ?? "/";
        var last = path[(path.LastIndexOf('/') + 1)..];
        return last.Length == 0 ? "Default.aspx" : last;
    }

    public static string SessionInfo(HttpContext context)
    {
        return $"{context.Session.GetString(SessionMachineNameKey)}, {context.Session.GetString(SessionStartTimeKey)}";
    }

    private static int Depth(HttpRequest request)
    {
        var path = request.Path.Value ?? "/";
        return Math.Max(0, path.Count(c => c == '/') - 1);
    }
}
