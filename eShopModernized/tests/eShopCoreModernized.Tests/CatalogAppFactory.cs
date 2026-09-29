using eShopCoreModernized.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace eShopCoreModernized.Tests
{
    public class CatalogAppFactory : WebApplicationFactory<Program>
    {
        public TestLoggerProvider LoggerProvider { get; } = new();

        public HealthStatus? ForcedReadinessStatus { get; init; }

        public Dictionary<string, string?> Settings { get; } = new()
        {
            ["AppSettings:UseMockData"] = "true",
            ["AppSettings:UseAzureStorage"] = "false",
            ["Deployment:Version"] = "9.9.9-test",
            ["Deployment:Track"] = "canary",
            ["Logging:LogLevel:eShopCoreModernized"] = "Debug",
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var (key, value) in Settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(LoggerProvider);
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddMvcCore().AddApplicationPart(typeof(ThrowingController).Assembly);

                if (ForcedReadinessStatus is { } status)
                {
                    services.AddHealthChecks().AddCheck(
                        "forced",
                        () => new HealthCheckResult(status, "forced by test"),
                        tags: new[] { HealthCheckTags.Ready });
                }
            });
        }
    }

    [ApiController]
    [Route("test/throw")]
    public class ThrowingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => throw new InvalidOperationException("boom");
    }
}
