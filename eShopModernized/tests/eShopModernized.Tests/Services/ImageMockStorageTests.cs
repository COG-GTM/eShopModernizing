using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Services
{
    public class ImageMockStorageTests
    {
        private static ImageMockStorage CreateStorage()
        {
            var environment = new Mock<IWebHostEnvironment>();
            var logger = new Mock<ILogger<ImageMockStorage>>();
            return new ImageMockStorage(environment.Object, logger.Object);
        }

        [Fact]
        public void BaseUrl_ReturnsPicsRoot()
        {
            Assert.Equal("/pics/", CreateStorage().BaseUrl());
        }

        [Fact]
        public void UrlDefaultImage_ReturnsDefaultPng()
        {
            Assert.Equal("/pics/default.png", CreateStorage().UrlDefaultImage());
        }

        [Fact]
        public void BuildUrlImage_WithPictureFileName_ReturnsItemScopedUrl()
        {
            var item = new CatalogItem { Id = 42, PictureFileName = "42.png" };

            Assert.Equal("/pics/42/42.png", CreateStorage().BuildUrlImage(item));
        }

        [Fact]
        public void BuildUrlImage_WithoutPictureFileName_ReturnsDefaultImage()
        {
            var item = new CatalogItem { Id = 42, PictureFileName = string.Empty };

            Assert.Equal("/pics/default.png", CreateStorage().BuildUrlImage(item));
        }

        [Fact]
        public async Task InitializeCatalogImagesAsync_CompletesWithoutError()
        {
            var exception = await Record.ExceptionAsync(() => CreateStorage().InitializeCatalogImagesAsync());

            Assert.Null(exception);
        }

        [Fact]
        public async Task UpdateImageAsync_CompletesWithoutError()
        {
            var exception = await Record.ExceptionAsync(() => CreateStorage().UpdateImageAsync(new CatalogItem { Id = 1 }));

            Assert.Null(exception);
        }

        [Fact]
        public async Task UploadTempImageAsync_ReturnsTempUrlWithFileName()
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.FileName).Returns("photo.png");

            var url = await CreateStorage().UploadTempImageAsync(file.Object, 7);

            Assert.Equal("/pics/temp/photo.png", url);
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var exception = Record.Exception(() => CreateStorage().Dispose());

            Assert.Null(exception);
        }
    }
}
