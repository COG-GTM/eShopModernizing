using Xunit;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopCoreModernized.Tests;

public class ImageMockStorageTests
{
    private static ImageMockStorage CreateStorage()
    {
        var environment = new Mock<IWebHostEnvironment>().Object;
        return new ImageMockStorage(environment, NullLogger<ImageMockStorage>.Instance);
    }

    [Fact]
    public void BaseUrl_ReturnsPicsRoot()
    {
        var storage = CreateStorage();

        Assert.Equal("/pics/", storage.BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_ReturnsDefaultPng()
    {
        var storage = CreateStorage();

        Assert.Equal("/pics/default.png", storage.UrlDefaultImage());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildUrlImage_ReturnsDefault_WhenPictureFileNameMissing(string? pictureFileName)
    {
        var storage = CreateStorage();
        var item = new CatalogItem { Id = 7, PictureFileName = pictureFileName! };

        Assert.Equal("/pics/default.png", storage.BuildUrlImage(item));
    }

    [Fact]
    public void BuildUrlImage_ReturnsItemUrl_WhenPictureFileNamePresent()
    {
        var storage = CreateStorage();
        var item = new CatalogItem { Id = 42, PictureFileName = "photo.png" };

        Assert.Equal("/pics/42/photo.png", storage.BuildUrlImage(item));
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_CompletesWithoutError()
    {
        var storage = CreateStorage();

        var exception = await Record.ExceptionAsync(() => storage.InitializeCatalogImagesAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateImageAsync_CompletesWithoutError()
    {
        var storage = CreateStorage();

        var exception = await Record.ExceptionAsync(() => storage.UpdateImageAsync(new CatalogItem { Id = 1 }));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var storage = CreateStorage();

        var exception = Record.Exception(() => storage.Dispose());

        Assert.Null(exception);
    }
}
