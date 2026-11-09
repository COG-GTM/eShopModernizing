using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// Status-code behaviour of the legacy System.Web pipeline: unhandled exceptions are 500 and unknown paths/actions are 404,
/// both with an HTML error page (bodies are not part of the contract; statuses are).
/// </summary>
public static class LegacyErrorHandling
{
    public static WebApplication UseLegacyErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context =>
            {
                var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return WriteErrorPageAsync(context, "Runtime Error",
                    error is null ? "An application error occurred on the server." : error.GetType().FullName + ": " + error.Message);
            },
        });
        app.UseStatusCodePages(context =>
        {
            var http = context.HttpContext;
            var status = http.Response.StatusCode;
            var description = status == StatusCodes.Status404NotFound
                ? "The resource cannot be found."
                : ReasonPhrases.GetReasonPhrase(status);
            return WriteErrorPageAsync(http, description, $"HTTP {status}. Requested URL: {http.Request.Path}");
        });
        return app;
    }

    private static Task WriteErrorPageAsync(HttpContext context, string title, string detail)
    {
        var appName = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/";
        context.Response.ContentType = "text/html; charset=utf-8";
        var html = "<!DOCTYPE html>\n<html>\n<head>\n<title>" + WebUtility.HtmlEncode(title) + "</title>\n</head>\n<body>\n" +
                   "<h1>Server Error in '" + WebUtility.HtmlEncode(appName) + "' Application</h1>\n" +
                   "<h2><i>" + WebUtility.HtmlEncode(title) + "</i></h2>\n" +
                   "<p>" + WebUtility.HtmlEncode(detail) + "</p>\n</body>\n</html>\n";
        return context.Response.WriteAsync(html);
    }
}
