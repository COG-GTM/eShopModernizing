using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class HealthControllerTests
    {
        private readonly Mock<ICatalogConfiguration> _config = new();
        private readonly Mock<ILogger<HealthController>> _logger = new();

        private static CatalogDBContext CreateDbContext(string name)
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseInMemoryDatabase(name)
                .Options;
            return new CatalogDBContext(options);
        }

        private HealthController CreateController(
            CatalogDBContext dbContext,
            BlobServiceClient? blobServiceClient = null)
        {
            return new HealthController(_config.Object, dbContext, _logger.Object, blobServiceClient);
        }

        private static IDictionary<string, object> GetServices(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var servicesProp = value.GetType().GetProperty("services");
            Assert.NotNull(servicesProp);
            return Assert.IsAssignableFrom<IDictionary<string, object>>(servicesProp!.GetValue(value)!);
        }

        private static string GetStatus(object section)
        {
            var statusProp = section.GetType().GetProperty("status");
            Assert.NotNull(statusProp);
            return (string)statusProp!.GetValue(section)!;
        }

        [Fact]
        public void Get_ReturnsHealthyStatus()
        {
            using var db = CreateDbContext(nameof(Get_ReturnsHealthyStatus));
            var controller = CreateController(db);

            var result = controller.Get();

            var ok = Assert.IsType<OkObjectResult>(result);
            var statusProp = ok.Value!.GetType().GetProperty("status");
            Assert.NotNull(statusProp);
            Assert.Equal("Healthy", statusProp!.GetValue(ok.Value));
        }

        [Fact]
        public async Task GetDetailed_DatabaseHealthy_ReturnsHealthyDatabaseSection()
        {
            using var db = CreateDbContext(nameof(GetDetailed_DatabaseHealthy_ReturnsHealthyDatabaseSection));
            _config.SetupGet(c => c.UseAzureStorage).Returns(false);
            var controller = CreateController(db);

            var result = await controller.GetDetailed();

            var services = GetServices(result);
            Assert.Equal("Healthy", GetStatus(services["database"]));
            Assert.True(services.ContainsKey("configuration"));
            Assert.False(services.ContainsKey("azureStorage"));
        }

        [Fact]
        public async Task GetDetailed_DatabaseUnhealthy_RecordsUnhealthyStatus()
        {
            var db = CreateDbContext(nameof(GetDetailed_DatabaseUnhealthy_RecordsUnhealthyStatus));
            _config.SetupGet(c => c.UseAzureStorage).Returns(false);
            var controller = CreateController(db);
            db.Dispose();

            var result = await controller.GetDetailed();

            var services = GetServices(result);
            Assert.Equal("Unhealthy", GetStatus(services["database"]));
        }

        [Fact]
        public async Task GetDetailed_AzureStorageEnabledWithNullClient_RecordsNotConfigured()
        {
            using var db = CreateDbContext(nameof(GetDetailed_AzureStorageEnabledWithNullClient_RecordsNotConfigured));
            _config.SetupGet(c => c.UseAzureStorage).Returns(true);
            var controller = CreateController(db, blobServiceClient: null);

            var result = await controller.GetDetailed();

            var services = GetServices(result);
            Assert.Equal("NotConfigured", GetStatus(services["azureStorage"]));
        }

        [Fact]
        public async Task GetDetailed_AzureStorageHealthy_RecordsHealthyStatus()
        {
            using var db = CreateDbContext(nameof(GetDetailed_AzureStorageHealthy_RecordsHealthyStatus));
            _config.SetupGet(c => c.UseAzureStorage).Returns(true);

            var containerClient = new Mock<BlobContainerClient>();
            var properties = BlobsModelFactory.BlobContainerProperties(
                DateTimeOffset.UtcNow,
                new ETag("0x1"),
                (LeaseStatus?)null,
                (LeaseState?)null,
                (LeaseDurationType?)null,
                (PublicAccessType?)null,
                (bool?)null,
                (bool?)null,
                new Dictionary<string, string>());
            var response = Response.FromValue(properties, Mock.Of<Response>());
            containerClient
                .Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var blobServiceClient = new Mock<BlobServiceClient>();
            blobServiceClient
                .Setup(b => b.GetBlobContainerClient("pics"))
                .Returns(containerClient.Object);

            var controller = CreateController(db, blobServiceClient.Object);

            var result = await controller.GetDetailed();

            var services = GetServices(result);
            Assert.Equal("Healthy", GetStatus(services["azureStorage"]));
        }

        [Fact]
        public async Task GetDetailed_AzureStorageThrows_RecordsUnhealthyStatus()
        {
            using var db = CreateDbContext(nameof(GetDetailed_AzureStorageThrows_RecordsUnhealthyStatus));
            _config.SetupGet(c => c.UseAzureStorage).Returns(true);

            var containerClient = new Mock<BlobContainerClient>();
            containerClient
                .Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException("storage down"));

            var blobServiceClient = new Mock<BlobServiceClient>();
            blobServiceClient
                .Setup(b => b.GetBlobContainerClient("pics"))
                .Returns(containerClient.Object);

            var controller = CreateController(db, blobServiceClient.Object);

            var result = await controller.GetDetailed();

            var services = GetServices(result);
            Assert.Equal("Unhealthy", GetStatus(services["azureStorage"]));
        }
    }
}
