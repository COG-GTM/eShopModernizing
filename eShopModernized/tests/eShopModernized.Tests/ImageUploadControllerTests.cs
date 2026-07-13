using eShopCoreModernized.Controllers;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class ImageUploadControllerTests
{
    private readonly Mock<IImageService> _imageService = new();

    private ImageUploadController CreateController() =>
        new(_imageService.Object, NullLogger<ImageUploadController>.Instance);

    private static IFormFile FakeFile(string fileName, long length)
    {
        var mock = new Mock<IFormFile>();
        mock.SetupGet(f => f.FileName).Returns(fileName);
        mock.SetupGet(f => f.Length).Returns(length);
        return mock.Object;
    }

    private static string? ReadImageUrl(object? value) =>
        value?.GetType().GetProperty("imageUrl")?.GetValue(value) as string;

    [Fact]
    public async Task UploadImage_WithValidPng_ReturnsOkWithImageUrl()
    {
        _imageService.Setup(i => i.UploadTempImageAsync(It.IsAny<IFormFile>(), 5))
            .ReturnsAsync("http://img/temp.png");

        var result = await CreateController().UploadImage(FakeFile("photo.png", 100), 5);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("http://img/temp.png", ReadImageUrl(ok.Value));
        _imageService.Verify(i => i.UploadTempImageAsync(It.IsAny<IFormFile>(), 5), Times.Once);
    }

    [Fact]
    public async Task UploadImage_WithNullFile_ReturnsBadRequest()
    {
        var result = await CreateController().UploadImage(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded", bad.Value);
        _imageService.Verify(i => i.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task UploadImage_WithEmptyFile_ReturnsBadRequest()
    {
        var result = await CreateController().UploadImage(FakeFile("photo.png", 0));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded", bad.Value);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("archive.zip")]
    [InlineData("script.exe")]
    public async Task UploadImage_WithDisallowedExtension_ReturnsBadRequest(string fileName)
    {
        var result = await CreateController().UploadImage(FakeFile(fileName, 100));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid file type. Only JPG, PNG, and GIF files are allowed.", bad.Value);
        _imageService.Verify(i => i.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task UploadImage_WhenServiceThrows_Returns500()
    {
        _imageService.Setup(i => i.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateController().UploadImage(FakeFile("photo.jpg", 100));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
    }

    [Fact]
    public void GetDefaultImage_ReturnsOkWithDefaultImageUrl()
    {
        _imageService.Setup(i => i.UrlDefaultImage()).Returns("http://img/default.png");

        var result = CreateController().GetDefaultImage();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("http://img/default.png", ReadImageUrl(ok.Value));
    }
}
