using System.Net;
using eShop.Catalog.Mvc.Hosting.Observability;
using eShop.Catalog.Data.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Mvc.Tests.S9_Ops;

public sealed class ObservabilityFactory : WebApplicationFactory<Program>
{
    public InMemoryLogSink Sink { get; } = new();

    public CatalogDataOptions? DataOptionsOverride { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Sink);
            if (DataOptionsOverride is not null)
            {
                services.RemoveAll<CatalogDataOptions>();
                services.AddSingleton(DataOptionsOverride);
            }
        });
    }
}

public class ObservabilityIntegrationTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";

    // Nothing listens on port 1: the connection is refused immediately.
    private static readonly CatalogDataOptions DatabaseDown = new()
    {
        UseMockData = false,
        ConnectionString = "Server=127.0.0.1,1;Database=Microsoft.eShopOnContainers.Services.CatalogDb;User Id=sa;Password=unused;Connect Timeout=1;Encrypt=False",
    };

    [Fact]
    public async Task Request_writes_one_structured_log_entry_with_method_path_status_and_w3c_trace_id()
    {
        await using var factory = new ObservabilityFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/s9-probe/missing?x=1");
        request.Headers.Add("traceparent", $"00-{TraceId}-b7ad6b7169203331-01");

        using var response = await client.SendAsync(request);

        var entries = await factory.Sink.WaitForAsync(e => e.EventId.Name == "RequestFinished" && Equals(e.Get("TraceId"), TraceId));
        var entry = Assert.Single(entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("GET", entry.Get("Method"));
        Assert.Equal("/s9-probe/missing", entry.Get("Path"));
        Assert.Equal((int)response.StatusCode, entry.Get("StatusCode"));
        Assert.Equal(TraceId, entry.Scope[RequestLogScope.TraceId]);
    }

    [Fact]
    public async Task Console_logging_is_json_with_scopes_and_utc_timestamps()
    {
        await using var factory = new ObservabilityFactory();

        var console = factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        var json = factory.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;
        var providers = factory.Services.GetServices<ILoggerProvider>().Select(p => p.GetType().Name).ToArray();

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.True(json.IncludeScopes);
        Assert.True(json.UseUtcTimestamp);
        Assert.Contains("ConsoleLoggerProvider", providers);
        Assert.DoesNotContain("DebugLoggerProvider", providers);
        Assert.DoesNotContain("EventSourceLoggerProvider", providers);
    }

    [Fact]
    public async Task Readiness_and_liveness_are_healthy_in_mock_mode()
    {
        await using var factory = new ObservabilityFactory();
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/healthz");
        using var ready = await client.GetAsync("/healthz/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Database_down_makes_readiness_unhealthy_while_liveness_stays_200()
    {
        await using var factory = new ObservabilityFactory { DataOptionsOverride = DatabaseDown };
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/healthz/ready");
        using var live = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Database_check_reports_missing_connection_string_as_unhealthy()
    {
        await using var factory = new ObservabilityFactory
        {
            DataOptionsOverride = new CatalogDataOptions { UseMockData = false, ConnectionString = "" },
        };
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/healthz/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }
}
