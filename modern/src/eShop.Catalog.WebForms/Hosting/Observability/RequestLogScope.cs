using System.Collections;
using System.Diagnostics;

namespace eShop.Catalog.WebForms.Hosting.Observability;

/// <summary>Request properties attached to every log entry written while a request is processed.</summary>
public sealed class RequestLogScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    public const string TraceId = "TraceId";
    public const string SpanId = "SpanId";
    public const string RequestMethod = "RequestMethod";
    public const string RequestPath = "RequestPath";

    private readonly KeyValuePair<string, object?>[] values;

    private RequestLogScope(KeyValuePair<string, object?>[] values)
    {
        this.values = values;
    }

    public int Count => values.Length;

    public KeyValuePair<string, object?> this[int index] => values[index];

    public static RequestLogScope Create(HttpContext context, Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new RequestLogScope(new KeyValuePair<string, object?>[]
        {
            new(TraceId, GetTraceId(context, activity)),
            new(SpanId, activity?.SpanId.ToHexString()),
            new(RequestMethod, context.Request.Method),
            new(RequestPath, context.Request.Path.Value),
        });
    }

    /// <summary>W3C trace id (32 hex chars) of the request activity; falls back to the ASP.NET Core request id.</summary>
    public static string GetTraceId(HttpContext context, Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(context);
        return activity is not null && activity.TraceId != default ? activity.TraceId.ToHexString() : context.TraceIdentifier;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Join(", ", values.Select(v => $"{v.Key}:{v.Value}"));
}
