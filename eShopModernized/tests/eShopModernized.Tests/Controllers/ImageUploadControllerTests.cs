using eShopCoreModernized.Controllers;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class ImageUploadControllerTests
    {
        private readonly Mock<IImageService> _imageService = new();
        private readonly Mock<ILogger<ImageUploadController>> _logger = new();

        private ImageUploadController CreateController() =>
            new(_imageService.Object, _logger.Object);

        private static Mock<IFormFile> CreateFile(string fileName, long length)
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.FileName).Returns(fileName);
            file.SetupGet(f => f.Length).Returns(length);
            return file;
        }

        [Fact]
        public async Task UploadImage_NullFile_ReturnsBadRequest()
        {
            var controller = CreateController();

            var result = await controller.UploadImage(null!, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No file uploaded", badRequest.Value);
        }

        [Fact]
        public async Task UploadImage_EmptyFile_ReturnsBadRequest()
        {
            var file = CreateFile("image.png", 0);
            var controller = CreateController();

            var result = await controller.UploadImage(file.Object, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("No file uploaded", badRequest.Value);
        }

        [Fact]
        public async Task UploadImage_DisallowedExtension_ReturnsBadRequest()
        {
            var file = CreateFile("document.txt", 10);
            var controller = CreateController();

            var result = await controller.UploadImage(file.Object, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid file type", badRequest.Value!.ToString());
            _imageService.Verify(
                s => s.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Theory]
        [InlineData("image.png")]
        [InlineData("image.jpg")]
        [InlineData("image.jpeg")]
        [InlineData("image.gif")]
        public async Task UploadImage_AllowedExtension_ReturnsOkWithImageUrl(string fileName)
        {
            const string expectedUrl = "/pics/temp/uploaded.png";
            var file = CreateFile(fileName, 123);
            _imageService
                .Setup(s => s.UploadTempImageAsync(file.Object, 42))
                .ReturnsAsync(expectedUrl);
            var controller = CreateController();

            var result = await controller.UploadImage(file.Object, 42);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedUrl, GetImageUrl(ok.Value));
            _imageService.Verify(s => s.UploadTempImageAsync(file.Object, 42), Times.Once);
        }

        [Fact]
        public async Task UploadImage_NullCatalogItemId_ReturnsOk()
        {
            const string expectedUrl = "/pics/temp/uploaded.png";
            var file = CreateFile("image.png", 50);
            _imageService
                .Setup(s => s.UploadTempImageAsync(file.Object, null))
                .ReturnsAsync(expectedUrl);
            var controller = CreateController();

            var result = await controller.UploadImage(file.Object);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedUrl, GetImageUrl(ok.Value));
        }

        [Fact]
        public async Task UploadImage_ServiceThrows_Returns500()
        {
            var file = CreateFile("image.png", 50);
            _imageService
                .Setup(s => s.UploadTempImageAsync(It.IsAny<IFormFile>(), It.IsAny<int?>()))
                .ThrowsAsync(new Exception("boom"));
            var controller = CreateController();

            var result = await controller.UploadImage(file.Object, 1);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }

        [Fact]
        public void GetDefaultImage_ReturnsOkWithDefaultUrl()
        {
            const string defaultUrl = "/pics/default.png";
            _imageService.Setup(s => s.UrlDefaultImage()).Returns(defaultUrl);
            var controller = CreateController();

            var result = controller.GetDefaultImage();

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(defaultUrl, GetImageUrl(ok.Value));
        }

        private static string? GetImageUrl(object? value)
        {
            Assert.NotNull(value);
            var property = value!.GetType().GetProperty("imageUrl");
            Assert.NotNull(property);
            return property!.GetValue(value) as string;
        }
    }
}
