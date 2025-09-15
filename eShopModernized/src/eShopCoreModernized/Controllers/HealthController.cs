using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Models;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ICatalogConfiguration _config;
        private readonly CatalogDBContext _dbContext;
        private readonly BlobServiceClient? _blobServiceClient;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            ICatalogConfiguration config,
            CatalogDBContext dbContext,
            ILogger<HealthController> logger,
            BlobServiceClient? blobServiceClient = null)
        {
            _config = config;
            _dbContext = dbContext;
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }

        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailed()
        {
            var healthStatus = new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                version = ".NET Core 6.0",
                services = await GetServiceHealthAsync()
            };

            return Ok(healthStatus);
        }

        private async Task<object> GetServiceHealthAsync()
        {
            var services = new Dictionary<string, object>();

            try
            {
                await _dbContext.Database.CanConnectAsync();
                services["database"] = new { status = "Healthy", type = "SQL Server" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                services["database"] = new { status = "Unhealthy", error = ex.Message };
            }

            if (_config.UseAzureStorage && _blobServiceClient != null)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient("pics");
                    await containerClient.GetPropertiesAsync();
                    services["azureStorage"] = new { status = "Healthy", type = "Blob Storage" };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Azure Storage health check failed");
                    services["azureStorage"] = new { status = "Unhealthy", error = ex.Message };
                }
            }
            else if (_config.UseAzureStorage)
            {
                services["azureStorage"] = new { status = "NotConfigured", message = "BlobServiceClient not available" };
            }

            services["configuration"] = new
            {
                useMockData = _config.UseMockData,
                useAzureStorage = _config.UseAzureStorage,
                useManagedIdentity = _config.UseManagedIdentity,
                useAzureActiveDirectory = _config.UseAzureActiveDirectory
            };

            return services;
        }
    }
}
