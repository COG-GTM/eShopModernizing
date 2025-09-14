using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using eShopCoreModernized.Models;
using eShopCoreModernized.Configuration;

namespace eShopCoreModernized.Services
{
    public class ImageAzureStorage : IImageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly ICatalogConfiguration _catalogConfiguration;
        private readonly ILogger<ImageAzureStorage> _logger;

        public ImageAzureStorage(
            BlobServiceClient blobServiceClient, 
            IConfiguration configuration,
            ICatalogConfiguration catalogConfiguration,
            ILogger<ImageAzureStorage> logger)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _catalogConfiguration = catalogConfiguration;
            _logger = logger;
        }

        public string BaseUrl()
        {
            return _blobServiceClient.Uri.ToString();
        }

        public string BuildUrlImage(CatalogItem item)
        {
            if (string.IsNullOrEmpty(item.PictureFileName))
                return UrlDefaultImage();

            return $"{_blobServiceClient.Uri}pics/{item.Id}/{item.PictureFileName}";
        }

        public void Dispose()
        {
        }

        public async Task InitializeCatalogImagesAsync()
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("pics");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    await blobClient.DeleteIfExistsAsync();
                }

                var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Pics");
                
                for (int i = 1; i <= 12; i++)
                {
                    var path = Path.Combine(webRoot, $"{i}.png");
                    if (File.Exists(path))
                    {
                        var blobName = $"{i}/{i}.png";
                        await UploadImageFromFileAsync(containerClient, blobName, path, "image/png");
                    }
                }

                var defaultImagePath = Path.Combine(webRoot, "default.png");
                if (File.Exists(defaultImagePath))
                {
                    await UploadImageFromFileAsync(containerClient, "temp/default.png", defaultImagePath, "image/png");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing catalog images in Azure Storage");
                throw;
            }
        }

        public async Task UpdateImageAsync(CatalogItem item)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("pics");
                
                if (string.IsNullOrEmpty(item.TempImageName))
                    return;

                var folder = item.TempImageName.Replace("/pics/", string.Empty);
                var tempBlobClient = containerClient.GetBlobClient(folder);

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: $"{item.Id}/"))
                {
                    var blobToDelete = containerClient.GetBlobClient(blobItem.Name);
                    await blobToDelete.DeleteIfExistsAsync();
                }

                var fileName = Path.GetFileName(item.TempImageName);
                var imageBlobClient = containerClient.GetBlobClient($"{item.Id}/{fileName}");

                var copyOperation = await imageBlobClient.StartCopyFromUriAsync(tempBlobClient.Uri);
                await copyOperation.WaitForCompletionAsync();
                
                await tempBlobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating image for catalog item {ItemId}", item.Id);
                throw;
            }
        }

        public async Task<string> UploadTempImageAsync(IFormFile file, int? catalogItemId)
        {
            try
            {
                string path = catalogItemId.HasValue ? $"{catalogItemId}/temp/" : $"temp/{Guid.NewGuid()}/";

                var containerClient = _blobServiceClient.GetBlobContainerClient("pics");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                
                var blobClient = containerClient.GetBlobClient(path + file.FileName.ToLower());

                var blobHttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType };
                
                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading temp image for catalog item {ItemId}", catalogItemId);
                throw;
            }
        }

        public string UrlDefaultImage()
        {
            return $"{_blobServiceClient.Uri}pics/temp/default.png";
        }

        private async Task UploadImageFromFileAsync(BlobContainerClient containerClient, string blobName, string filePath, string contentType)
        {
            try
            {
                using var fileStream = File.OpenRead(filePath);
                var blobClient = containerClient.GetBlobClient(blobName);
                var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
                await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image from file {FilePath} to blob {BlobName}", filePath, blobName);
                throw;
            }
        }
    }
}
