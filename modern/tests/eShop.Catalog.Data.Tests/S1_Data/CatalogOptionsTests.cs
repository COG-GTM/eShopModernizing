using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static eShop.Catalog.Data.Tests.S1_Data.TestSupport;

namespace eShop.Catalog.Data.Tests.S1_Data;

public class CatalogOptionsTests
{
    private const string ValidConnectionString = "Server=sql,1433;Database=CatalogDb;User Id=sa;Password=x;TrustServerCertificate=True";

    [Fact]
    public void Defaults_match_legacy_web_config_when_keys_are_missing()
    {
        var options = CatalogDataOptions.FromConfiguration(Config());

        Assert.True(options.UseMockData);
        Assert.False(options.UseCustomizationData);
        Assert.Null(options.ConnectionString);
    }

    [Theory]
    [InlineData("false", "false", false, false)]
    [InlineData("False", "True", false, true)]
    [InlineData("true", "true", true, true)]
    public void Binds_legacy_boolean_settings(string useMock, string useCustomization, bool expectedMock, bool expectedCustomization)
    {
        var options = CatalogDataOptions.FromConfiguration(Config(("UseMockData", useMock), ("UseCustomizationData", useCustomization)));

        Assert.Equal(expectedMock, options.UseMockData);
        Assert.Equal(expectedCustomization, options.UseCustomizationData);
    }

    [Fact]
    public void Binds_connection_string_from_environment_style_key()
    {
        var options = CatalogDataOptions.FromConfiguration(Config(("ConnectionStrings:CatalogDBContext", ValidConnectionString)));

        Assert.Equal(ValidConnectionString, options.ConnectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sql_mode_without_connection_string_fails_fast(string? connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddCatalogData(Config(("UseMockData", "false"), ("ConnectionStrings:CatalogDBContext", connectionString))));

        Assert.Contains("ConnectionStrings:CatalogDBContext", ex.Message);
    }

    [Theory]
    [InlineData("this is not a connection string")]
    [InlineData("Server=sql;Bogus Keyword=1;Database=x")]
    public void Sql_mode_with_malformed_connection_string_fails_fast(string connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CatalogPersistenceServiceCollectionExtensions.Validate(new CatalogDataOptions { UseMockData = false, ConnectionString = connectionString }));

        Assert.Contains("not a valid SQL Server connection string", ex.Message);
    }

    [Theory]
    [InlineData("Database=CatalogDb;User Id=sa;Password=x")]
    [InlineData("Server=sql;User Id=sa;Password=x")]
    public void Sql_mode_requires_server_and_database(string connectionString)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CatalogPersistenceServiceCollectionExtensions.Validate(new CatalogDataOptions { UseMockData = false, ConnectionString = connectionString }));

        Assert.Contains("must set both a server", ex.Message);
    }

    [Fact]
    public void Mock_mode_ignores_connection_string_and_needs_no_database()
    {
        using var provider = new ServiceCollection()
            .AddCatalogData(Config(("UseMockData", "true"), ("ConnectionStrings:CatalogDBContext", "garbage")))
            .BuildServiceProvider();

        Assert.IsType<CatalogServiceMock>(provider.GetRequiredService<ICatalogService>());
        Assert.Empty(provider.GetServices<IHostedService>());
        Assert.Null(provider.GetService<CatalogDbContext>());
        Assert.Equal(12, provider.GetRequiredService<ICatalogService>().GetCatalogItemsPaginated(10, 0).TotalItems);
    }

    [Fact]
    public void Sql_mode_registers_scoped_ef_service_seeder_and_startup_initializer()
    {
        var services = new ServiceCollection().AddLogging()
            .AddCatalogData(Config(("UseMockData", "false"), ("ConnectionStrings:CatalogDBContext", ValidConnectionString)));
        using var provider = services.BuildServiceProvider();

        Assert.Contains(services, d => d.ServiceType == typeof(ICatalogService) && d.ImplementationType == typeof(CatalogService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(CatalogDbContext) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(CatalogSeeder));
        Assert.IsType<CatalogDatabaseInitializerHostedService>(Assert.Single(provider.GetServices<IHostedService>()));
        using var scope = provider.CreateScope();
        Assert.IsType<CatalogService>(scope.ServiceProvider.GetRequiredService<ICatalogService>());
        Assert.False(provider.GetRequiredService<CatalogDataOptions>().UseMockData);
    }
}
