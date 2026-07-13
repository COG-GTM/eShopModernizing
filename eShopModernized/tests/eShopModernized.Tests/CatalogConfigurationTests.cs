using eShopCoreModernized.Configuration;
using Microsoft.Extensions.Configuration;

namespace eShopModernized.Tests;

public class CatalogConfigurationTests
{
    private static ICatalogConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new CatalogConfiguration(configuration);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void UseMockData_ReflectsConfiguredBooleanValue(string configured, bool expected)
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AppSettings:UseMockData"] = configured
        });

        Assert.Equal(expected, config.UseMockData);
    }

    [Fact]
    public void BooleanProperties_MapToExpectedConfigurationKeys()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
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
    public void BooleanProperties_DefaultToFalse_WhenKeysMissing()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>());

        Assert.False(config.UseMockData);
        Assert.False(config.UseAzureStorage);
        Assert.False(config.UseManagedIdentity);
        Assert.False(config.UseCustomizationData);
        Assert.False(config.UseAzureActiveDirectory);
    }

    [Fact]
    public void StringProperties_ReturnConfiguredValues()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Azure:StorageConnectionString"] = "storage-cs",
            ["Azure:ApplicationInsights:InstrumentationKey"] = "ikey",
            ["Azure:ApplicationInsights:ConnectionString"] = "ai-cs",
            ["Azure:ActiveDirectory:ClientId"] = "client-id",
            ["Azure:ActiveDirectory:TenantId"] = "tenant-id",
            ["Azure:ActiveDirectory:PostLogoutRedirectUri"] = "https://example.com/logout"
        });

        Assert.Equal("storage-cs", config.StorageConnectionString);
        Assert.Equal("ikey", config.AppInsightsInstrumentationKey);
        Assert.Equal("ai-cs", config.AppInsightsConnectionString);
        Assert.Equal("client-id", config.AzureActiveDirectoryClientId);
        Assert.Equal("tenant-id", config.AzureActiveDirectoryTenant);
        Assert.Equal("https://example.com/logout", config.PostLogoutRedirectUri);
    }

    [Fact]
    public void StringProperties_FallBackToEmptyString_WhenKeysMissing()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>());

        Assert.Equal(string.Empty, config.StorageConnectionString);
        Assert.Equal(string.Empty, config.AppInsightsInstrumentationKey);
        Assert.Equal(string.Empty, config.AppInsightsConnectionString);
        Assert.Equal(string.Empty, config.AzureActiveDirectoryClientId);
        Assert.Equal(string.Empty, config.AzureActiveDirectoryTenant);
        Assert.Equal(string.Empty, config.PostLogoutRedirectUri);
    }
}
