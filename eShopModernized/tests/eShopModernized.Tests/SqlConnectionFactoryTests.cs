using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace eShopModernized.Tests;

public class SqlConnectionFactoryTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void AppSettingsFactory_CreateConnection_UsesConfiguredConnectionString()
    {
        const string connectionString = "Server=(local);Database=CatalogDb;Integrated Security=true;";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CatalogDBContext"] = connectionString
        });
        var factory = new AppSettingsSqlConnectionFactory(configuration);

        using var connection = factory.CreateConnection();

        Assert.NotNull(connection);
        Assert.Equal(connectionString, connection.ConnectionString);
    }

    [Fact]
    public void AppSettingsFactory_CreateConnection_ReturnsEmptyConnectionString_WhenNotConfigured()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var factory = new AppSettingsSqlConnectionFactory(configuration);

        using var connection = factory.CreateConnection();

        Assert.NotNull(connection);
        Assert.Equal(string.Empty, connection.ConnectionString);
    }

    [Fact]
    public void AppSettingsFactory_CreateConnection_ReturnsNewInstanceEachCall()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CatalogDBContext"] = "Server=(local);Database=CatalogDb;"
        });
        var factory = new AppSettingsSqlConnectionFactory(configuration);

        using var first = factory.CreateConnection();
        using var second = factory.CreateConnection();

        Assert.NotSame(first, second);
    }

    // ManagedIdentitySqlConnectionFactory.CreateConnection() acquires an Azure AD access
    // token via DefaultAzureCredential, which requires a live Azure identity environment.
    // We only cover the constructible path here; exercising CreateConnection() would need a
    // real managed identity / Azure credential and is therefore excluded from hermetic tests.
    [Fact]
    public void ManagedIdentityFactory_CanBeConstructed_WithoutAzureEnvironment()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CatalogDBContext"] = "Server=tcp:example.database.windows.net;Database=CatalogDb;"
        });

        var factory = new ManagedIdentitySqlConnectionFactory(configuration);

        Assert.IsAssignableFrom<ISqlConnectionFactory>(factory);
    }
}
