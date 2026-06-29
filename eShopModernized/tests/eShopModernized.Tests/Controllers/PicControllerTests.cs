using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class PicControllerTests : IDisposable
    {
        private readonly Mock<ICatalogService> _service = new();
        private readonly Mock<IWebHostEnvironment> _environment = new();
        private readonly string _webRoot;

        public PicControllerTests()
        {
            _webRoot = Path.Combine(Path.GetTempPath(), "pic-controller-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_webRoot, "Pics"));
            _environment.SetupGet(e => e.WebRootPath).Returns(_webRoot);
            _environment.SetupGet(e => e.ContentRootPath).Returns(_webRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }

        private PicController CreateController()
        {
            var logger = new Mock<ILogger<PicController>>();
            return new PicController(_service.Object, logger.Object, _environment.Object);
        }

        private string WritePicture(string fileName)
        {
            var path = Path.Combine(_webRoot, "Pics", fileName);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            return path;
        }

        [Fact]
        public async Task Index_NonPositiveId_ReturnsBadRequest()
        {
            var controller = CreateController();

            var result = await controller.Index(0);

            Assert.IsType<BadRequestResult>(result);
            _service.Verify(s => s.FindCatalogItemAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Index_UnknownItem_ReturnsNotFound()
        {
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync((CatalogItem?)null);
            var controller = CreateController();

            var result = await controller.Index(5);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Index_ItemFoundButFileMissing_ReturnsNotFound()
        {
            var item = new CatalogItem { Id = 5, PictureFileName = "missing.png" };
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Index(5);

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData("1.png", "image/png")]
        [InlineData("2.jpg", "image/jpeg")]
        [InlineData("3.jpeg", "image/jpeg")]
        [InlineData("4.gif", "image/gif")]
        [InlineData("5.bmp", "image/bmp")]
        [InlineData("6.tiff", "image/tiff")]
        [InlineData("7.wmf", "image/wmf")]
        [InlineData("8.jp2", "image/jp2")]
        [InlineData("9.svg", "image/svg+xml")]
        [InlineData("10.unknown", "application/octet-stream")]
        public async Task Index_ItemFoundAndFileExists_ReturnsFileWithExpectedMimeType(string fileName, string expectedMime)
        {
            WritePicture(fileName);
            var item = new CatalogItem { Id = 5, PictureFileName = fileName };
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Index(5);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal(expectedMime, fileResult.ContentType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, fileResult.FileContents);
        }

        [Fact]
        public async Task Index_WebRootPathNull_FallsBackToContentRootPath()
        {
            _environment.SetupGet(e => e.WebRootPath).Returns((string?)null!);
            WritePicture("fallback.png");
            var item = new CatalogItem { Id = 5, PictureFileName = "fallback.png" };
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Index(5);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/png", fileResult.ContentType);
        }
    }
}
