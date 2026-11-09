using System.Net.Http.Headers;
using EShop.Catalog.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EShop.Storefront.Tests.Infrastructure;

/// <summary>Ticket slices the tests depend on (modernization/TICKETS.md).</summary>
public enum Slice
{
    CatalogData,   // T1
    MvcCatalog,    // T2
    LegacyApi,     // T3
    RazorCatalog,  // T4
    Hosting,       // T5
}

/// <summary>In-process host for one storefront app in mock mode, same configuration as the parity run (Production, UseMockData=true).</summary>
public sealed class StorefrontFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("UseMockData", "true");
        builder.UseSetting("UseCustomizationData", "false");
    }
}

/// <summary>
/// One app per test class (xUnit class fixture), so every class starts from a freshly seeded mock catalog.
/// Set <c>ESHOP_TEST_MVC_URL</c> / <c>ESHOP_TEST_WEBFORMS_URL</c> to run the same tests against a live server instead
/// (e.g. the legacy apps on Mono :5100/:5101); the server must be freshly started.
/// Tests for a slice whose ticket is still the scaffold stub are skipped, unless <c>ESHOP_TESTS_REQUIRE_ALL=1</c>.
/// </summary>
public abstract class StorefrontFixture<TProgram> : IAsyncDisposable, IDisposable where TProgram : class
{
    private readonly StorefrontFactory<TProgram>? _factory;
    private readonly bool _isMvc;

    protected StorefrontFixture(string liveUrlVariable, bool isMvc)
    {
        _isMvc = isMvc;
        var live = Environment.GetEnvironmentVariable(liveUrlVariable);
        if (!string.IsNullOrWhiteSpace(live))
        {
            var baseUri = new Uri(live.TrimEnd('/') + "/");
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true };
            Client = new StorefrontClient(new HttpClient(handler) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(120) });
            IsLive = true;
            return;
        }

        _factory = new StorefrontFactory<TProgram>();
        var http = _factory.CreateDefaultClient(new Uri("http://localhost/"), new ServerErrorHandler(), new CookieContainerHandler());
        Client = new StorefrontClient(http);
    }

    public StorefrontClient Client { get; }

    public bool IsLive { get; }

    public IServiceProvider Services => _factory?.Services ?? throw new InvalidOperationException("No in-process host in live mode.");

    public static bool RequireAll => Environment.GetEnvironmentVariable("ESHOP_TESTS_REQUIRE_ALL") is "1" or "true";

    /// <summary>Skips the calling test while one of the slices it needs is still the scaffold stub.</summary>
    public void Require(params Slice[] slices)
    {
        if (IsLive || RequireAll)
        {
            return;
        }

        var missing = slices.Where(s => !IsImplemented(s)).ToArray();
        Skip.If(missing.Length > 0, $"scaffold stub, not merged yet: {string.Join(", ", missing.Select(Describe))}");
    }

    public bool IsImplemented(Slice slice)
    {
        if (IsLive)
        {
            return true;
        }

        var endpoints = Services.GetRequiredService<EndpointDataSource>().Endpoints;
        return slice switch
        {
            Slice.CatalogData => HasCatalogService(Services),
            Slice.MvcCatalog => endpoints.Any(e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName == "Catalog"),
            Slice.LegacyApi => endpoints.Any(e =>
                string.Equals(e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName, "Brands", StringComparison.OrdinalIgnoreCase)
                || RawText(e).StartsWith("api/brands", StringComparison.OrdinalIgnoreCase)),
            Slice.RazorCatalog => endpoints.Any(e =>
                (e.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath ?? string.Empty).StartsWith("/Catalog/", StringComparison.OrdinalIgnoreCase)
                || RawText(e).StartsWith("Catalog/Create", StringComparison.OrdinalIgnoreCase)),
            Slice.Hosting => Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider
                .GetFileInfo(_isMvc ? "Content/Site.css" : "Content/site.css").Exists,
            _ => false,
        };
    }

    internal static bool HasCatalogService(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetService<ICatalogService>() is not null;
    }

    private static string RawText(Endpoint e) => ((e as RouteEndpoint)?.RoutePattern.RawText ?? string.Empty).TrimStart('/');

    private static string Describe(Slice s) => s switch
    {
        Slice.CatalogData => "T1 catalog data",
        Slice.MvcCatalog => "T2 MVC catalog",
        Slice.LegacyApi => "T3 legacy API",
        Slice.RazorCatalog => "T4 Razor catalog",
        Slice.Hosting => "T5 hosting/static",
        _ => s.ToString(),
    };

    public void Dispose() => _factory?.Dispose();

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}

public sealed class MvcAppFixture() : StorefrontFixture<EShop.Storefront.Mvc.Program>("ESHOP_TEST_MVC_URL", isMvc: true);

public sealed class WebFormsAppFixture() : StorefrontFixture<EShop.Storefront.WebForms.Program>("ESHOP_TEST_WEBFORMS_URL", isMvc: false);

/// <summary>TestServer rethrows unhandled app exceptions to the caller; Kestrel answers 500. Mirror Kestrel.</summary>
internal sealed class ServerErrorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                RequestMessage = request,
                Content = new StringContent(string.Empty) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } },
            };
        }
    }
}
