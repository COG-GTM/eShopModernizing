using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class HealthControllerTests : IDisposable
{
    private readonly Mock<ICatalogConfiguration> _config = new();
    private readonly CatalogDBContext _dbContext;

    public HealthControllerTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDBContext>()
            .UseInMemoryDatabase("health-" + Guid.NewGuid().ToString("N"))
            .Options;
        _dbContext = new CatalogDBContext(options);
    }

    private HealthController CreateController() =>
        new(_config.Object, _dbContext, NullLogger<HealthController>.Instance);

    private static T? ReadProp<T>(object? value, string name) =>
        (T?)value?.GetType().GetProperty(name)?.GetValue(value);

    private static object? ReadService(object? servicesValue, string key)
    {
        var services = Assert.IsAssignableFrom<IDictionary<string, object>>(servicesValue);
        return services.TryGetValue(key, out var entry) ? entry : null;
    }

    [Fact]
    public void Get_ReturnsOkWithHealthyStatus()
    {
        var result = CreateController().Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Healthy", ReadProp<string>(ok.Value, "status"));
    }

    [Fact]
    public async Task GetDetailed_ReturnsOkWithStatusAndVersion()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(false);

        var result = await CreateController().GetDetailed();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Healthy", ReadProp<string>(ok.Value, "status"));
        Assert.False(string.IsNullOrEmpty(ReadProp<string>(ok.Value, "version")));
        Assert.NotNull(ReadProp<object>(ok.Value, "services"));
    }

    [Fact]
    public async Task GetDetailed_WhenDatabaseReachable_ReportsHealthyDatabase()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(false);

        var result = await CreateController().GetDetailed();

        var ok = Assert.IsType<OkObjectResult>(result);
        var services = ReadProp<object>(ok.Value, "services");
        var database = ReadService(services, "database");
        Assert.NotNull(database);
        Assert.Equal("Healthy", ReadProp<string>(database, "status"));
    }

    [Fact]
    public async Task GetDetailed_WhenAzureStorageEnabledWithoutClient_ReportsNotConfigured()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);

        var result = await CreateController().GetDetailed();

        var ok = Assert.IsType<OkObjectResult>(result);
        var services = ReadProp<object>(ok.Value, "services");
        var azure = ReadService(services, "azureStorage");
        Assert.NotNull(azure);
        Assert.Equal("NotConfigured", ReadProp<string>(azure, "status"));
    }

    public void Dispose() => _dbContext.Dispose();
}
