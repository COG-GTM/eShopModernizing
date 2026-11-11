using System.Diagnostics;
using eShop.Catalog.WebForms.Hosting.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.WebForms.Tests.S9_Ops;

public sealed class RequestLoggingMiddlewareTests : IDisposable
{
    private readonly InMemoryLogSink sink = new();
    private readonly ILoggerFactory loggerFactory;

    public RequestLoggingMiddlewareTests()
    {
        loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(sink).SetMinimumLevel(LogLevel.Trace));
    }

    public void Dispose() => loggerFactory.Dispose();

    [Fact]
    public async Task Writes_one_entry_with_method_path_status_and_trace_id()
    {
        using var activity = StartActivity();
        var context = NewContext("GET", "/Catalog/Details/7");
        var middleware = NewMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(typeof(RequestLoggingMiddleware).FullName, entry.Category);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("RequestFinished", entry.EventId.Name);
        Assert.Equal("GET", entry.Get("Method"));
        Assert.Equal("/Catalog/Details/7", entry.Get("Path"));
        Assert.Equal(404, entry.Get("StatusCode"));
        Assert.Equal(activity.TraceId.ToHexString(), entry.Get("TraceId"));
        Assert.IsType<double>(entry.Get("ElapsedMs"));
        Assert.Equal(activity.TraceId.ToHexString(), entry.Scope[RequestLogScope.TraceId]);
        Assert.Equal("/Catalog/Details/7", entry.Scope[RequestLogScope.RequestPath]);
    }

    [Fact]
    public async Task Entries_logged_downstream_share_the_request_scope()
    {
        using var activity = StartActivity();
        var downstream = loggerFactory.CreateLogger("Downstream");
        var middleware = NewMiddleware(_ =>
        {
            downstream.LogInformation("loading catalog");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(NewContext("GET", "/"));

        var entry = Assert.Single(sink.Entries, e => e.Category == "Downstream");
        Assert.Equal(activity.TraceId.ToHexString(), entry.Scope[RequestLogScope.TraceId]);
        Assert.Equal("GET", entry.Scope[RequestLogScope.RequestMethod]);
        Assert.Equal("/", entry.Scope[RequestLogScope.RequestPath]);
    }

    [Fact]
    public async Task Unhandled_exception_is_logged_as_500_and_rethrown()
    {
        using var activity = StartActivity();
        var middleware = NewMiddleware(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(NewContext("POST", "/Catalog/Create")));

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(500, entry.Get("StatusCode"));
        Assert.Equal("POST", entry.Get("Method"));
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/healthz/ready")]
    public async Task Health_probes_are_logged_at_debug(string path)
    {
        var middleware = NewMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(NewContext("GET", path));

        Assert.Equal(LogLevel.Debug, Assert.Single(sink.Entries).Level);
    }

    private static Activity StartActivity()
    {
        var activity = new Activity("test-request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        return activity.Start();
    }

    private static DefaultHttpContext NewContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private RequestLoggingMiddleware NewMiddleware(RequestDelegate next) =>
        new(next, loggerFactory.CreateLogger<RequestLoggingMiddleware>());
}
