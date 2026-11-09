using System.Diagnostics;

namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// Replaces the log4net LogicalThreadContext properties set in Application_BeginRequest ("activityid", "requestinfo"):
/// every log entry written while the request runs carries ActivityId, RequestPath and RequestInfo in its scope.
/// </summary>
public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingScopeMiddleware> _logger;
    private readonly string _beginRequestMessage;

    public RequestLoggingScopeMiddleware(RequestDelegate next, ILogger<RequestLoggingScopeMiddleware> logger, string beginRequestMessage)
    {
        _next = next;
        _logger = logger;
        _beginRequestMessage = beginRequestMessage;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var rawUrl = request.PathBase + request.Path + request.QueryString;
        using (_logger.BeginScope("ActivityId:{ActivityId} RequestPath:{RequestPath} RequestInfo:{RequestInfo}",
                   Activity.Current?.Id ?? context.TraceIdentifier, request.Path.Value, $"{rawUrl}, {request.Headers.UserAgent}"))
        {
            _logger.LogDebug("{Message}", _beginRequestMessage);
            await _next(context);
        }
    }
}
