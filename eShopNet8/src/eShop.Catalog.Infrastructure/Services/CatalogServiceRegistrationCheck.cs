using eShop.Catalog.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.Catalog.Infrastructure.Services;

// UseMockData=false must never fall back to the mock silently: if no SQL-backed ICatalogService was registered
// (AddCatalogData), host start-up fails with an explicit message instead of every request failing later.
internal sealed class CatalogServiceRegistrationCheck : IHostedService
{
    private readonly IServiceProviderIsService serviceProviderIsService;

    public CatalogServiceRegistrationCheck(IServiceProviderIsService serviceProviderIsService)
    {
        this.serviceProviderIsService = serviceProviderIsService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!serviceProviderIsService.IsService(typeof(ICatalogService)))
        {
            throw new InvalidOperationException(
                "UseMockData=false but no SQL-backed ICatalogService is registered. The EF Core data layer " +
                "(AddCatalogData) must register CatalogDBContext and CatalogService and the connection string " +
                "'ConnectionStrings:CatalogDBContext' must point at a reachable SQL Server. " +
                "Set UseMockData=true (or Catalog__UseMockData=true) to run on the in-memory mock catalog.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
