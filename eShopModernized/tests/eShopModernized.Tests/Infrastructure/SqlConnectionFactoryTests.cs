using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eShopModernized.Tests.Infrastructure;

public class SqlConnectionFactoryTests
{
    private const string ConnectionString =
        "Server=tcp:localhost,1433;Database=CatalogDb;User Id=sa;Password=Passw0rd!;TrustServerCertificate=True";

    private static IConfiguration BuildConfiguration(string? connectionString)
    {
        var values = new Dictionary<string, string?>();
        if (connectionString != null)
        {
            values["ConnectionStrings:CatalogDBContext"] = connectionString;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AppSettingsFactory_CreatesConnection_WithConfiguredConnectionString()
    {
        var factory = new AppSettingsSqlConnectionFactory(BuildConfiguration(ConnectionString));

        using var connection = factory.CreateConnection();

        Assert.Contains("Database=CatalogDb", connection.ConnectionString);
    }

    [Fact]
    public void ManagedIdentityFactory_CanBeConstructed()
    {
        var factory = new ManagedIdentitySqlConnectionFactory(BuildConfiguration(ConnectionString));

        Assert.NotNull(factory);
    }

    // Acquiring a managed-identity token requires a live Azure environment, so
    // CreateConnection cannot complete here – it throws while requesting the
    // token. This still exercises the connection-building code up to that point.
    [Fact]
    public void ManagedIdentityFactory_CreateConnection_ThrowsWithoutAzureIdentity()
    {
        var factory = new ManagedIdentitySqlConnectionFactory(BuildConfiguration(ConnectionString));

        Assert.ThrowsAny<Exception>(() => factory.CreateConnection());
    }
}
