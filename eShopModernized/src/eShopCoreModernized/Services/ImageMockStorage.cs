using eShopCoreModernized.Models;

namespace eShopCoreModernized.Services
{
    public class ImageMockStorage : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageMockStorage> _logger;

        public ImageMockStorage(IWebHostEnvironment environment, ILogger<ImageMockStorage> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public string BaseUrl()
        {
            return "/pics/";
        }

        public string BuildUrlImage(CatalogItem item)
        {
            if (string.IsNullOrEmpty(item.PictureFileName))
                return UrlDefaultImage();

            return $"/pics/{item.Id}/{item.PictureFileName}";
        }

        public void Dispose()
        {
        }

        public Task InitializeCatalogImagesAsync()
        {
            _logger.LogInformation("Mock image storage - InitializeCatalogImagesAsync called");
            return Task.CompletedTask;
        }

        public Task UpdateImageAsync(CatalogItem item)
        {
            _logger.LogInformation("Mock image storage - UpdateImageAsync called for item {ItemId}", item.Id);
            return Task.CompletedTask;
        }

        public Task<string> UploadTempImageAsync(IFormFile file, int? catalogItemId)
        {
            _logger.LogInformation("Mock image storage - UploadTempImageAsync called for item {ItemId}", catalogItemId);
            var mockUrl = $"/pics/temp/{file.FileName}";
            return Task.FromResult(mockUrl);
        }

        public string UrlDefaultImage()
        {
            return "/pics/default.png";
        }
    }
}
