using System.Text;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Services;

public class ImageMockStorageTests
{
    private readonly Mock<IWebHostEnvironment> _environment = new();
    private readonly Mock<ILogger<ImageMockStorage>> _logger = new();

    private ImageMockStorage CreateStorage() => new(_environment.Object, _logger.Object);

    [Fact]
    public void BaseUrl_ReturnsPicsPath()
    {
        Assert.Equal("/pics/", CreateStorage().BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_ReturnsDefaultPath()
    {
        Assert.Equal("/pics/default.png", CreateStorage().UrlDefaultImage());
    }

    [Fact]
    public void BuildUrlImage_ReturnsDefault_WhenNoPicture()
    {
        var item = new CatalogItem { Id = 1, PictureFileName = string.Empty };

        Assert.Equal("/pics/default.png", CreateStorage().BuildUrlImage(item));
    }

    [Fact]
    public void BuildUrlImage_ReturnsItemPath_WhenPicturePresent()
    {
        var item = new CatalogItem { Id = 7, PictureFileName = "7.png" };

        Assert.Equal("/pics/7/7.png", CreateStorage().BuildUrlImage(item));
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_CompletesSuccessfully()
    {
        await CreateStorage().InitializeCatalogImagesAsync();
    }

    [Fact]
    public async Task UpdateImageAsync_CompletesSuccessfully()
    {
        await CreateStorage().UpdateImageAsync(new CatalogItem { Id = 1 });
    }

    [Fact]
    public async Task UploadTempImageAsync_ReturnsMockUrl()
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns("photo.png");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("x")));

        var url = await CreateStorage().UploadTempImageAsync(file.Object, 3);

        Assert.Equal("/pics/temp/photo.png", url);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        CreateStorage().Dispose();
    }
}
