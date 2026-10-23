using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Core.Configuration;

public static class CatalogSettingsServiceCollectionExtensions
{
    // Defaults live in the "Catalog" section (appsettings.json: Catalog:UseMockData), overridable with
    // `Catalog__UseMockData=false` / `--Catalog:UseMockData=false`. The legacy-style root keys
    // (`UseMockData=false` env var, `--UseMockData=false`) are not set by any shipped file and win when present.
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
