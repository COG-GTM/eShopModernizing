using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.DependencyInjection
{
    // Logs the selected catalog mode once when the host starts (legacy had no equivalent line; it read UseMockData
    // silently in Global.asax) and warns if the ICatalogService lifetime drifts from the legacy Autofac registration.
    internal sealed class CatalogModeStartupLogger : IHostedService
    {
        private readonly CatalogServiceMode mode;
        private readonly ILogger<CatalogModeStartupLogger> logger;

        public CatalogModeStartupLogger(CatalogServiceMode mode, ILogger<CatalogModeStartupLogger> logger)
        {
            this.mode = mode;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (mode.UseMockData)
            {
                logger.LogInformation(
                    "Catalog running in '{CatalogMode}' mode (UseMockData=true): in-memory {CatalogService} ({Lifetime}), mutations persist for the process lifetime.",
                    mode.Name, mode.Implementation, mode.Lifetime);
            }
            else
            {
                logger.LogInformation(
                    "Catalog running in '{CatalogMode}' mode (UseMockData=false): {CatalogService} ({Lifetime}) on SQL Server.",
                    mode.Name, mode.Implementation, mode.Lifetime);
            }

            if (mode.Lifetime != mode.ExpectedLifetime)
            {
                logger.LogWarning(
                    "ICatalogService is registered as {Lifetime} but the legacy application registered it as {ExpectedLifetime} in '{CatalogMode}' mode.",
                    mode.Lifetime, mode.ExpectedLifetime, mode.Name);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
