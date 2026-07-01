using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopModernized.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class HealthControllerTests
{
    private readonly Mock<ICatalogConfiguration> _config = new();
    private readonly Mock<ILogger<HealthController>> _logger = new();

    private static string Serialize(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonSerializer.Serialize(ok.Value);
    }

    [Fact]
    public void Get_ReturnsHealthy()
    {
        var controller = new HealthController(_config.Object, InMemoryContext.Create(), _logger.Object);

        var json = Serialize(controller.Get());

        Assert.Contains("Healthy", json);
    }

    [Fact]
    public async Task GetDetailed_ReportsHealthyDatabase_WhenAzureStorageDisabled()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(false);
        var controller = new HealthController(_config.Object, InMemoryContext.Create(), _logger.Object);

        var json = Serialize(await controller.GetDetailed());

        Assert.Contains("\"database\"", json);
        Assert.Contains("Healthy", json);
        Assert.DoesNotContain("azureStorage", json);
    }

    [Fact]
    public async Task GetDetailed_ReportsUnhealthyDatabase_WhenContextDisposed()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(false);
        var context = InMemoryContext.Create();
        context.Dispose();
        var controller = new HealthController(_config.Object, context, _logger.Object);

        var json = Serialize(await controller.GetDetailed());

        Assert.Contains("Unhealthy", json);
    }

    [Fact]
    public async Task GetDetailed_ReportsNotConfigured_WhenAzureEnabledButNoClient()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);
        var controller = new HealthController(_config.Object, InMemoryContext.Create(), _logger.Object, null);

        var json = Serialize(await controller.GetDetailed());

        Assert.Contains("NotConfigured", json);
    }

    [Fact]
    public async Task GetDetailed_ReportsUnhealthyStorage_WhenBlobClientThrows()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("no access"));

        var blob = new Mock<BlobServiceClient>();
        blob.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        var controller = new HealthController(_config.Object, InMemoryContext.Create(), _logger.Object, blob.Object);

        var json = Serialize(await controller.GetDetailed());

        Assert.Contains("Unhealthy", json);
    }

    [Fact]
    public async Task GetDetailed_ReportsHealthyStorage_WhenBlobClientResponds()
    {
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);

        var properties = BlobsModelFactory.BlobContainerProperties(
            lastModified: DateTimeOffset.UtcNow, eTag: new ETag("etag"));
        var response = Response.FromValue(properties, Mock.Of<Response>());

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var blob = new Mock<BlobServiceClient>();
        blob.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        var controller = new HealthController(_config.Object, InMemoryContext.Create(), _logger.Object, blob.Object);

        var json = Serialize(await controller.GetDetailed());

        Assert.Contains("Blob Storage", json);
    }
}
