using eShopModernizedWebForms.Models;
using System;
using System.IO;

namespace eShopModernizedWebForms.Services
{
    public interface IImageService: IDisposable
    {
        string UploadTempImage(Stream content, string fileName, string contentType, int? catalogItemId);
        string BaseUrl();
        void UpdateImage(CatalogItem item);
        string UrlDefaultImage();
        string BuildUrlImage(CatalogItem item);
        void InitializeCatalogImages();

    }
}
