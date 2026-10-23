using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using eShop.Catalog.Core.Services;

namespace eShopNet8.IntegrationTests.Parity;

// A target the flows are replayed against: an in-process WebApplicationFactory or an external base URL.
public interface IParityTarget : IAsyncDisposable
{
    string BaseUrl { get; }

    // Restart contract (CONTRACT.md section 7): fresh app + fresh mock data, never renders a catalog page.
    Task RestartAsync();

    // One cookie session ("requests.Session()"); redirects are not followed unless asked (snapshots follow them).
    HttpClient NewSession(bool followRedirects = false);

    // Non-null when the ticket that implements this flow is not part of the build yet (scaffold stub still in place).
    string? PendingReason(FlowSpec flow);
}

public sealed class InProcessTarget<TEntryPoint> : IParityTarget where TEntryPoint : class
{
    // The parity contract is recorded in mock mode against a production-like host (containers run Production).
    public static string Environment => System.Environment.GetEnvironmentVariable("ESHOP_PARITY_ENVIRONMENT") ?? "Production";

    private readonly string _app;
    private WebApplicationFactory<TEntryPoint>? _factory;
    private Features? _features;

    public InProcessTarget(string app) => _app = app;

    public string BaseUrl => "http://localhost";

    private sealed record Features(bool CatalogService, bool UiPending, bool ApiMapped);

    public async Task RestartAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        _factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(b => b.UseEnvironment(Environment));
        _features = Inspect(_factory.Services);
    }

    private static Features Inspect(IServiceProvider services)
    {
        bool catalogService;
        using (var scope = services.CreateScope())
        {
            catalogService = scope.ServiceProvider.GetService<ICatalogService>() is not null;
        }
        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var hasActions = endpoints.Any(e => e.Metadata.GetMetadata<ActionDescriptor>() is not null);
        // The frozen scaffold maps a text placeholder on "/" from the T3/T5 stub; real ports map controllers / Razor Pages.
        var placeholder = endpoints.Any(e => e.RoutePattern.RawText == "/" && e.Metadata.GetMetadata<ActionDescriptor>() is null);
        var api = endpoints.Any(e => (e.RoutePattern.RawText ?? "").TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase));
        return new Features(catalogService, placeholder && !hasActions, api);
    }

    public HttpClient NewSession(bool followRedirects = false)
    {
        if (_factory is null) throw new InvalidOperationException("RestartAsync first");
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = followRedirects,
            HandleCookies = true,
            BaseAddress = new Uri(BaseUrl + "/"),
        });
    }

    public string? PendingReason(FlowSpec flow)
    {
        var f = _features ?? throw new InvalidOperationException("RestartAsync first");
        if (!f.CatalogService) return "T2 not in this build: no ICatalogService registered (scaffold stub)";
        if (_app == "mvc" && flow.IsApi && !f.ApiMapped)
            return "T4 not in this build: no /api endpoints mapped (scaffold stub)";
        // Data-state snapshots read the catalog list and Create pages, so they need the UI ticket even for API flows.
        if (f.UiPending && (!flow.IsApi || flow.Snapshot))
            return _app == "mvc" ? "T3 not in this build: scaffold placeholder on /" : "T5 not in this build: scaffold placeholder on /";
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
    }
}

// Replays against a running app (e.g. the legacy Mono apps on 5100/5101, or the .NET 8 apps on 5200/5201).
public sealed class ExternalTarget : IParityTarget
{
    private readonly string? _restartCmd;

    public ExternalTarget(string baseUrl, string? restartCmd)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _restartCmd = restartCmd;
    }

    public string BaseUrl { get; }

    public async Task RestartAsync()
    {
        if (string.IsNullOrWhiteSpace(_restartCmd)) return;
        var psi = new ProcessStartInfo("bash", ["-c", _restartCmd]) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"restart command failed ({p.ExitCode}): {await stdout} {await stderr}");
    }

    public HttpClient NewSession(bool followRedirects = false) =>
        new(new HttpClientHandler
        {
            AllowAutoRedirect = followRedirects,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        })
        { Timeout = TimeSpan.FromSeconds(180) };

    public string? PendingReason(FlowSpec flow) => null;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
