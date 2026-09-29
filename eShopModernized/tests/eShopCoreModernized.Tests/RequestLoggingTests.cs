using System.Net;
using System.Text.Json;
using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Logging;

namespace eShopCoreModernized.Tests
{
    public class RequestLoggingTests
    {
        private const int RequestCompletedEventId = 1000;

        [Fact]
        public async Task Valid_incoming_correlation_id_is_echoed_and_logged()
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Catalog");
            request.Headers.Add(RequestLoggingMiddleware.CorrelationIdHeader, "order-42.abc");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("order-42.abc", Single(response, RequestLoggingMiddleware.CorrelationIdHeader));
            Assert.Equal("9.9.9-test", Single(response, RequestLoggingMiddleware.AppVersionHeader));
            Assert.Equal("canary", Single(response, RequestLoggingMiddleware.DeploymentTrackHeader));

            var log = Assert.Single(RequestLogs(factory), l => (string?)l.State["RequestPath"] == "/Catalog");
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal("GET", log.State["RequestMethod"]);
            Assert.Equal(200, log.State["StatusCode"]);
            Assert.IsType<double>(log.State["ElapsedMs"]);
            Assert.Equal("order-42.abc", log.Scope["CorrelationId"]);
            Assert.Equal("eshop-core-modernized", log.Scope["ServiceName"]);
            Assert.Equal("9.9.9-test", log.Scope["ServiceVersion"]);
            Assert.Equal("canary", log.Scope["DeploymentTrack"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("bad value with spaces")]
        [InlineData("inject\"quote")]
        public async Task Invalid_correlation_id_is_replaced(string incoming)
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            request.Headers.TryAddWithoutValidation(RequestLoggingMiddleware.CorrelationIdHeader, incoming);

            using var response = await client.SendAsync(request);

            var correlationId = Single(response, RequestLoggingMiddleware.CorrelationIdHeader);
            Assert.False(string.IsNullOrWhiteSpace(correlationId));
            Assert.NotEqual(incoming, correlationId);
        }

        [Fact]
        public async Task Overlong_correlation_id_is_replaced()
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            var incoming = new string('a', 129);
            request.Headers.Add(RequestLoggingMiddleware.CorrelationIdHeader, incoming);

            using var response = await client.SendAsync(request);

            Assert.NotEqual(incoming, Single(response, RequestLoggingMiddleware.CorrelationIdHeader));
        }

        [Fact]
        public async Task Successful_health_probes_log_at_debug()
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();

            await client.GetAsync("/health/ready");

            var log = Assert.Single(RequestLogs(factory), l => (string?)l.State["RequestPath"] == "/health/ready");
            Assert.Equal(LogLevel.Debug, log.Level);
        }

        [Fact]
        public async Task Unhandled_exception_returns_problem_details_with_correlation_id_and_logs_error()
        {
            await using var factory = new CatalogAppFactory();
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/test/throw");
            request.Headers.Add(RequestLoggingMiddleware.CorrelationIdHeader, "fail-1");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("boom", content);
            using var body = JsonDocument.Parse(content);
            Assert.Equal("fail-1", body.RootElement.GetProperty("correlationId").GetString());

            var log = Assert.Single(RequestLogs(factory), l => (string?)l.State["RequestPath"] == "/test/throw");
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.Equal(500, log.State["StatusCode"]);

            Assert.Contains(
                factory.LoggerProvider.Logs,
                l => l.Exception is InvalidOperationException && (string?)l.Scope["CorrelationId"] == "fail-1");
        }

        private static IEnumerable<CapturedLog> RequestLogs(CatalogAppFactory factory) =>
            factory.LoggerProvider.Logs.Where(l => l.EventId.Id == RequestCompletedEventId);

        private static string Single(HttpResponseMessage response, string header) =>
            Assert.Single(response.Headers.GetValues(header));
    }
}
