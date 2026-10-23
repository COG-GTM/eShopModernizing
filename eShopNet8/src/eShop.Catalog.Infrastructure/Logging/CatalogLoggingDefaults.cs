namespace eShop.Catalog.Infrastructure.Logging;

// Code defaults for the "Logging" configuration, replacing log4net.xml + Global.asax Application_BeginRequest.
// They are layered *below* the app's own "Logging" section, so appsettings/env/CLI still win key by key.
// - Console output with scopes: ASP.NET Core already opens a per-request scope (RequestId, RequestPath, ConnectionId)
//   and, with ActivityTrackingOptions, adds the request Activity TraceId/SpanId - the equivalent of the legacy
//   "activityid" (Trace.CorrelationManager.ActivityId) LogicalThreadContext property.
// - "Request starting HTTP/1.1 GET http://host/path?query" (Microsoft.AspNetCore.Hosting.Diagnostics, Information) is the
//   per-request entry equivalent to the legacy Debug "WebApplication_BeginRequest" + "requestinfo" (RawUrl). The legacy
//   User-Agent part is intentionally not logged (untrusted header, log forging / data minimisation).
public static class CatalogLoggingDefaults
{
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss,fff ";

    public static IReadOnlyDictionary<string, string?> Values { get; } = new Dictionary<string, string?>
    {
        ["Console:FormatterName"] = "simple",
        ["Console:FormatterOptions:IncludeScopes"] = "true",
        ["Console:FormatterOptions:TimestampFormat"] = TimestampFormat,
        ["LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information",
    };
}
