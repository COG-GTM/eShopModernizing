using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Data.Tests.S0_Scaffold;

public class AddCatalogDataTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void UseMockData_registers_singleton_mock()
    {
        using var provider = new ServiceCollection().AddCatalogData(Config(("UseMockData", "true"))).BuildServiceProvider();

        var first = provider.GetRequiredService<ICatalogService>();
        Assert.IsType<CatalogServiceMock>(first);
        Assert.Same(first, provider.GetRequiredService<ICatalogService>());
        Assert.True(provider.GetRequiredService<CatalogDataOptions>().UseMockData);
    }

    [Fact]
    public void Options_read_legacy_setting_names()
    {
        var options = CatalogDataOptions.FromConfiguration(Config(
            ("UseMockData", "false"), ("UseCustomizationData", "true"), ("ConnectionStrings:CatalogDBContext", "Server=x")));

        Assert.False(options.UseMockData);
        Assert.True(options.UseCustomizationData);
        Assert.Equal("Server=x", options.ConnectionString);
    }
}
