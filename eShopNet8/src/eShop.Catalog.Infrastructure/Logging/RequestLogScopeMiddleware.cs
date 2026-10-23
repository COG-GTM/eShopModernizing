using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.Logging;

// Equivalent of the legacy Global.asax Application_BeginRequest: every log entry written while the request runs carries
// "activityid" (legacy: Trace.CorrelationManager.ActivityId; here the request Activity trace id) and "requestinfo"
// (legacy: RawUrl + ", " + UserAgent), and a Debug "WebApplication_BeginRequest" entry is written per request.
public sealed class RequestLogScopeMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestLogScopeMiddleware> logger;

    public RequestLogScopeMiddleware(RequestDelegate next, ILogger<RequestLogScopeMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activityId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
        var requestInfo = context.Request.GetEncodedPathAndQuery() + ", " + context.Request.Headers.UserAgent;

        using (logger.BeginScope(new RequestLogScope(activityId, requestInfo)))
        {
            logger.LogDebug("WebApplication_BeginRequest");
            await next(context);
        }
    }
}
