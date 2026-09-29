using Azure.Storage.Blobs;
using eShopCoreModernized.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.HealthChecks
{
    public sealed class BlobStorageHealthCheck : IHealthCheck
    {
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageHealthCheck(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object> { ["container"] = ImageAzureStorage.PicsContainerName };
            try
            {
                var container = _blobServiceClient.GetBlobContainerClient(ImageAzureStorage.PicsContainerName);
                var exists = await container.ExistsAsync(cancellationToken);
                return exists.Value
                    ? HealthCheckResult.Healthy("Blob container is reachable", data)
                    : new HealthCheckResult(context.Registration.FailureStatus, "Blob container does not exist", data: data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "Blob storage is unreachable", ex, data);
            }
        }
    }
}
