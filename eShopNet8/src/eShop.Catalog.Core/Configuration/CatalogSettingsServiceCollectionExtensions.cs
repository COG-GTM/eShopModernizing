using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Core.Configuration;

public static class CatalogSettingsServiceCollectionExtensions
{
    // Root-level keys (UseMockData, UseCustomizationData) win over the "Catalog" section so that
    // `--UseMockData=false` and `UseMockData=false` env vars work like the legacy appSettings keys.
    public static IServiceCollection AddCatalogSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CatalogSettings>()
            .Bind(configuration.GetSection(CatalogSettings.SectionName))
            .Configure(s => Read(configuration, s));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CatalogSettings>>().Value);
        return services;
    }

    public static CatalogSettings GetCatalogSettings(this IConfiguration configuration)
    {
        var s = new CatalogSettings();
        configuration.GetSection(CatalogSettings.SectionName).Bind(s);
        Read(configuration, s);
        return s;
    }

    private static void Read(IConfiguration configuration, CatalogSettings s)
    {
        if (bool.TryParse(configuration["UseMockData"], out var mock)) s.UseMockData = mock;
        if (bool.TryParse(configuration["UseCustomizationData"], out var custom)) s.UseCustomizationData = custom;
    }
}
