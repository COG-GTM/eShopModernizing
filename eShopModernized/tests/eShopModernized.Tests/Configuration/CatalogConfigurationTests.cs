using eShopCoreModernized.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eShopModernized.Tests.Configuration;

public class CatalogConfigurationTests
{
    private static CatalogConfiguration Build(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new CatalogConfiguration(configuration);
    }

    [Fact]
    public void BooleanFlags_ReflectConfiguredValues()
    {
        var config = Build(new Dictionary<string, string?>
        {
            ["AppSettings:UseMockData"] = "true",
            ["AppSettings:UseAzureStorage"] = "true",
            ["AppSettings:UseAzureManagedIdentity"] = "true",
            ["AppSettings:UseCustomizationData"] = "true",
            ["AppSettings:UseAzureActiveDirectory"] = "true"
        });

        Assert.True(config.UseMockData);
        Assert.True(config.UseAzureStorage);
        Assert.True(config.UseManagedIdentity);
        Assert.True(config.UseCustomizationData);
        Assert.True(config.UseAzureActiveDirectory);
    }

    [Fact]
    public void BooleanFlags_DefaultToFalse_WhenMissing()
    {
        var config = Build(new Dictionary<string, string?>());

        Assert.False(config.UseMockData);
        Assert.False(config.UseAzureStorage);
        Assert.False(config.UseManagedIdentity);
        Assert.False(config.UseCustomizationData);
        Assert.False(config.UseAzureActiveDirectory);
    }

    [Fact]
    public void StringProperties_ReflectConfiguredValues()
    {
        var config = Build(new Dictionary<string, string?>
        {
            ["Azure:StorageConnectionString"] = "storage-cs",
            ["Azure:ApplicationInsights:InstrumentationKey"] = "ikey",
            ["Azure:ApplicationInsights:ConnectionString"] = "ai-cs",
            ["Azure:ActiveDirectory:ClientId"] = "client-id",
            ["Azure:ActiveDirectory:TenantId"] = "tenant-id",
            ["Azure:ActiveDirectory:PostLogoutRedirectUri"] = "https://logout"
        });

        Assert.Equal("storage-cs", config.StorageConnectionString);
        Assert.Equal("ikey", config.AppInsightsInstrumentationKey);
        Assert.Equal("ai-cs", config.AppInsightsConnectionString);
        Assert.Equal("client-id", config.AzureActiveDirectoryClientId);
        Assert.Equal("tenant-id", config.AzureActiveDirectoryTenant);
        Assert.Equal("https://logout", config.PostLogoutRedirectUri);
    }

    [Fact]
    public void StringProperties_DefaultToEmpty_WhenMissing()
    {
        var config = Build(new Dictionary<string, string?>());

        Assert.Equal(string.Empty, config.StorageConnectionString);
        Assert.Equal(string.Empty, config.AppInsightsInstrumentationKey);
        Assert.Equal(string.Empty, config.AppInsightsConnectionString);
        Assert.Equal(string.Empty, config.AzureActiveDirectoryClientId);
        Assert.Equal(string.Empty, config.AzureActiveDirectoryTenant);
        Assert.Equal(string.Empty, config.PostLogoutRedirectUri);
    }
}
