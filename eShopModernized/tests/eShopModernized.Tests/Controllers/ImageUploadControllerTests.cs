using System.Text;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class ImageUploadControllerTests
{
    private readonly Mock<IImageService> _imageService = new();
    private readonly Mock<ILogger<ImageUploadController>> _logger = new();

    private ImageUploadController CreateController() => new(_imageService.Object, _logger.Object);

    private static IFormFile CreateFormFile(string fileName, long length = 10, string contentType = "image/png")
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns(fileName);
        file.SetupGet(f => f.Length).Returns(length);
        file.SetupGet(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("data")));
        return file.Object;
    }

    [Fact]
    public async Task UploadImage_ReturnsBadRequest_WhenFileNull()
    {
        var result = await CreateController().UploadImage(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded", bad.Value);
    }

    [Fact]
    public async Task UploadImage_ReturnsBadRequest_WhenFileEmpty()
    {
        var result = await CreateController().UploadImage(CreateFormFile("a.png", length: 0));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadImage_ReturnsBadRequest_WhenExtensionNotAllowed()
    {
        var result = await CreateController().UploadImage(CreateFormFile("malware.exe"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid file type", bad.Value!.ToString());
    }

    [Fact]
    public async Task UploadImage_ReturnsOk_WhenValid()
    {
        _imageService.Setup(s => s.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()))
            .ReturnsAsync("/pics/temp/a.png");

        var result = await CreateController().UploadImage(CreateFormFile("a.png"), 5);

        Assert.IsType<OkObjectResult>(result);
        _imageService.Verify(s => s.UploadTempImageAsync(It.IsAny<IFormFile>(), 5), Times.Once);
    }

    [Fact]
    public async Task UploadImage_Returns500_WhenServiceThrows()
    {
        _imageService.Setup(s => s.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateController().UploadImage(CreateFormFile("a.png"));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
    }

    [Fact]
    public void GetDefaultImage_ReturnsOk()
    {
        _imageService.Setup(s => s.UrlDefaultImage()).Returns("/pics/default.png");

        var result = CreateController().GetDefaultImage();

        Assert.IsType<OkObjectResult>(result);
    }
}
