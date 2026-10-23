using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Data;
using eShop.Catalog.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.UnitTests;

// Builds service providers through the production registration path (AddCatalogSettings/AddCatalogData/AddCatalogServices),
// so the tests compile against the frozen Core contracts and exercise whatever T1/T2 register.
internal static class TestServices
{
    public static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    public static IServiceCollection Collection(IConfiguration cfg)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCatalogSettings(cfg);
        services.AddCatalogData(cfg);
        services.AddCatalogServices(cfg);
        return services;
    }

    // Fresh in-memory mock catalog (UseMockData=true, the default); skips when T2 is not in the build.
    public static ICatalogService MockService()
    {
        var sp = Collection(Config()).BuildServiceProvider();
        var service = sp.GetService<ICatalogService>();
        Skip.If(service is null, "T2 not in this build: AddCatalogServices registers no ICatalogService (scaffold stub)");
        return service!;
    }
}
