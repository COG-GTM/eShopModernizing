using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.Tests
{
    public class HealthEndpointTests
    {
        [Theory]
        [InlineData("/health/live", "self")]
        [InlineData("/health/ready", "catalog-data")]
        [InlineData("/health", "self")]
        [InlineData("/health", "catalog-data")]
        [InlineData("/health/detailed", "catalog-data")]
        public async Task Health_endpoints_report_healthy_with_deployment_metadata(string path, string expectedCheck)
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.Equal("Healthy", root.GetProperty("status").GetString());
            Assert.Equal("eshop-core-modernized", root.GetProperty("service").GetString());
            Assert.Equal("9.9.9-test", root.GetProperty("version").GetString());
            Assert.Equal("canary", root.GetProperty("deploymentTrack").GetString());
            Assert.Contains(
                root.GetProperty("checks").EnumerateArray(),
                check => check.GetProperty("name").GetString() == expectedCheck);
        }

        [Fact]
        public async Task Liveness_only_runs_live_checks()
        {
            await using var factory = new CatalogAppFactory { ForcedReadinessStatus = HealthStatus.Unhealthy };
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/health/live");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var names = body.RootElement.GetProperty("checks").EnumerateArray()
                .Select(check => check.GetProperty("name").GetString());
            Assert.Equal(new[] { "self" }, names);
        }

        [Theory]
        [InlineData("/health/ready")]
        [InlineData("/health")]
        [InlineData("/api/health")]
        [InlineData("/api/health/detailed")]
        public async Task Unhealthy_dependency_returns_503(string path)
        {
            await using var factory = new CatalogAppFactory { ForcedReadinessStatus = HealthStatus.Unhealthy };
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Degraded_dependency_stays_in_rotation()
        {
            await using var factory = new CatalogAppFactory { ForcedReadinessStatus = HealthStatus.Degraded };
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Degraded", body.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Legacy_api_health_contract_is_preserved()
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();

            using var summary = JsonDocument.Parse(await client.GetStringAsync("/api/health"));
            Assert.Equal("Healthy", summary.RootElement.GetProperty("status").GetString());
            Assert.True(summary.RootElement.TryGetProperty("timestamp", out _));
            Assert.Equal("9.9.9-test", summary.RootElement.GetProperty("version").GetString());

            using var detailed = JsonDocument.Parse(await client.GetStringAsync("/api/health/detailed"));
            var services = detailed.RootElement.GetProperty("services");
            Assert.Equal("Healthy", services.GetProperty("catalog-data").GetProperty("status").GetString());
            Assert.True(services.GetProperty("configuration").GetProperty("useMockData").GetBoolean());
        }

        [Fact]
        public async Task Health_report_does_not_leak_connection_strings()
        {
            await using var factory = new CatalogAppFactory();
            factory.Settings["ConnectionStrings:CatalogDBContext"] = "Server=db;Password=SuperSecret123";
            using var client = factory.CreateClient();

            var full = await client.GetStringAsync("/health");
            var detailed = await client.GetStringAsync("/api/health/detailed");

            Assert.DoesNotContain("SuperSecret123", full);
            Assert.DoesNotContain("SuperSecret123", detailed);
        }
    }
}
