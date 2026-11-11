using System.Diagnostics;

namespace eShop.Catalog.WebForms.Hosting.Observability;

/// <summary>Writes one structured log entry per request: method, path, status, elapsed time and W3C trace id.</summary>
public sealed partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestLoggingMiddleware> logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        var traceId = RequestLogScope.GetTraceId(context, activity);
        var level = ObservabilityExtensions.IsHealthProbe(context.Request.Path) ? LogLevel.Debug : LogLevel.Information;
        var start = Stopwatch.GetTimestamp();

        using var scope = logger.BeginScope(RequestLogScope.Create(context, activity));
        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            var elapsedMs = Math.Round(Stopwatch.GetElapsedTime(start).TotalMilliseconds, 1);
            LogRequest(logger, level, context.Request.Method, context.Request.Path.Value ?? "/", statusCode, elapsedMs, traceId);
        }
    }

    [LoggerMessage(EventId = 1, EventName = "RequestFinished",
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (trace {TraceId})")]
    private static partial void LogRequest(
        ILogger logger, LogLevel level, string method, string path, int statusCode, double elapsedMs, string traceId);
}
