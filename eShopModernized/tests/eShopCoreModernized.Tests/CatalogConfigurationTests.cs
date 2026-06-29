using eShopCoreModernized.Configuration;
using Microsoft.Extensions.Configuration;

namespace eShopCoreModernized.Tests;

public class CatalogConfigurationTests
{
    private static CatalogConfiguration CreateConfig(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new CatalogConfiguration(configuration);
    }

    [Fact]
    public void UseMockData_ReturnsTrue_WhenSet()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "AppSettings:UseMockData", "true" }
        });

        Assert.True(config.UseMockData);
    }

    [Fact]
    public void UseMockData_ReturnsFalse_WhenNotSet()
    {
        var config = CreateConfig(new Dictionary<string, string?>());

        Assert.False(config.UseMockData);
    }

    [Fact]
    public void UseAzureStorage_ReadsFromConfig()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "AppSettings:UseAzureStorage", "true" }
        });

        Assert.True(config.UseAzureStorage);
    }

    [Fact]
    public void UseManagedIdentity_ReadsFromConfig()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "AppSettings:UseAzureManagedIdentity", "true" }
        });

        Assert.True(config.UseManagedIdentity);
    }

    [Fact]
    public void UseCustomizationData_ReadsFromConfig()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "AppSettings:UseCustomizationData", "true" }
        });

        Assert.True(config.UseCustomizationData);
    }

    [Fact]
    public void UseAzureActiveDirectory_ReadsFromConfig()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "AppSettings:UseAzureActiveDirectory", "true" }
        });

        Assert.True(config.UseAzureActiveDirectory);
    }

    [Fact]
    public void StorageConnectionString_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:StorageConnectionString", "DefaultEndpointsProtocol=https;AccountName=test" }
        });

        Assert.Equal("DefaultEndpointsProtocol=https;AccountName=test", config.StorageConnectionString);
    }

    [Fact]
    public void StorageConnectionString_ReturnsEmpty_WhenNotSet()
    {
        var config = CreateConfig(new Dictionary<string, string?>());

        Assert.Equal(string.Empty, config.StorageConnectionString);
    }

    [Fact]
    public void AppInsightsInstrumentationKey_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:ApplicationInsights:InstrumentationKey", "test-key" }
        });

        Assert.Equal("test-key", config.AppInsightsInstrumentationKey);
    }

    [Fact]
    public void AppInsightsConnectionString_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:ApplicationInsights:ConnectionString", "InstrumentationKey=abc" }
        });

        Assert.Equal("InstrumentationKey=abc", config.AppInsightsConnectionString);
    }

    [Fact]
    public void AzureActiveDirectoryClientId_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:ActiveDirectory:ClientId", "client-123" }
        });

        Assert.Equal("client-123", config.AzureActiveDirectoryClientId);
    }

    [Fact]
    public void AzureActiveDirectoryTenant_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:ActiveDirectory:TenantId", "tenant-456" }
        });

        Assert.Equal("tenant-456", config.AzureActiveDirectoryTenant);
    }

    [Fact]
    public void PostLogoutRedirectUri_ReturnsValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            { "Azure:ActiveDirectory:PostLogoutRedirectUri", "https://example.com/signout" }
        });

        Assert.Equal("https://example.com/signout", config.PostLogoutRedirectUri);
    }
}
