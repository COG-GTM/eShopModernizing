using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace eShopCoreModernized.Tests;

public class ImageMockStorageTests
{
    private readonly ImageMockStorage _storage;

    public ImageMockStorageTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns("/wwwroot");
        var logger = new Mock<ILogger<ImageMockStorage>>();
        _storage = new ImageMockStorage(env.Object, logger.Object);
    }

    [Fact]
    public void BaseUrl_ReturnsPicsPath()
    {
        Assert.Equal("/pics/", _storage.BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_ReturnsDefaultPng()
    {
        Assert.Equal("/pics/default.png", _storage.UrlDefaultImage());
    }

    [Fact]
    public void BuildUrlImage_ReturnsCorrectPath()
    {
        var item = new CatalogItem { Id = 5, PictureFileName = "test.png" };

        var url = _storage.BuildUrlImage(item);

        Assert.Equal("/pics/5/test.png", url);
    }

    [Fact]
    public void BuildUrlImage_ReturnsDefault_WhenPictureFileNameIsEmpty()
    {
        var item = new CatalogItem { Id = 1, PictureFileName = "" };

        var url = _storage.BuildUrlImage(item);

        Assert.Equal("/pics/default.png", url);
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_CompletesSuccessfully()
    {
        await _storage.InitializeCatalogImagesAsync();
    }

    [Fact]
    public async Task UpdateImageAsync_CompletesSuccessfully()
    {
        var item = new CatalogItem { Id = 1 };

        await _storage.UpdateImageAsync(item);
    }

    [Fact]
    public async Task UploadTempImageAsync_ReturnsMockUrl()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("photo.jpg");

        var result = await _storage.UploadTempImageAsync(fileMock.Object, 7);

        Assert.Equal("/pics/temp/photo.jpg", result);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var exception = Record.Exception(() => _storage.Dispose());

        Assert.Null(exception);
    }
}
