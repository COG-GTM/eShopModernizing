using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace eShopModernized.Tests;

public class ImageMockStorageTests
{
    private static ImageMockStorage CreateService()
    {
        var environment = new Mock<IWebHostEnvironment>();
        var logger = new Mock<ILogger<ImageMockStorage>>();
        return new ImageMockStorage(environment.Object, logger.Object);
    }

    [Fact]
    public void BaseUrl_ReturnsPicsRoot()
    {
        var service = CreateService();

        Assert.Equal("/pics/", service.BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_ReturnsDefaultPngPath()
    {
        var service = CreateService();

        Assert.Equal("/pics/default.png", service.UrlDefaultImage());
    }

    [Fact]
    public void BuildUrlImage_WithPictureFileName_ReturnsItemScopedPath()
    {
        var service = CreateService();
        var item = new CatalogItem { Id = 7, PictureFileName = "photo.png" };

        Assert.Equal("/pics/7/photo.png", service.BuildUrlImage(item));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void BuildUrlImage_WithoutPictureFileName_ReturnsDefaultImage(string? pictureFileName)
    {
        var service = CreateService();
        var item = new CatalogItem { Id = 7, PictureFileName = pictureFileName! };

        Assert.Equal(service.UrlDefaultImage(), service.BuildUrlImage(item));
    }

    [Fact]
    public async Task UploadTempImageAsync_ReturnsTempUrlBasedOnFileName()
    {
        var service = CreateService();
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns("upload.png");

        var result = await service.UploadTempImageAsync(file.Object, catalogItemId: 42);

        Assert.Equal("/pics/temp/upload.png", result);
    }

    [Fact]
    public async Task UploadTempImageAsync_WithNullCatalogItemId_StillReturnsTempUrl()
    {
        var service = CreateService();
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns("no-id.png");

        var result = await service.UploadTempImageAsync(file.Object, catalogItemId: null);

        Assert.Equal("/pics/temp/no-id.png", result);
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_CompletesWithoutError()
    {
        var service = CreateService();

        await service.InitializeCatalogImagesAsync();
    }

    [Fact]
    public async Task UpdateImageAsync_CompletesWithoutError()
    {
        var service = CreateService();
        var item = new CatalogItem { Id = 3 };

        await service.UpdateImageAsync(item);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var service = CreateService();

        var exception = Record.Exception(() => service.Dispose());

        Assert.Null(exception);
    }
}
