using eShopCoreModernized.Models;

namespace eShopCoreModernized.Services
{
    public interface IImageService : IDisposable
    {
        Task<string> UploadTempImageAsync(IFormFile file, int? catalogItemId);
        string BaseUrl();
        Task UpdateImageAsync(CatalogItem item);
        string UrlDefaultImage();
        string BuildUrlImage(CatalogItem item);
        Task InitializeCatalogImagesAsync();
    }
}
