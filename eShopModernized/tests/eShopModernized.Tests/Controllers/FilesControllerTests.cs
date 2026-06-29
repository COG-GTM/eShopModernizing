using System.Text.Json;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class FilesControllerTests
    {
        private readonly Mock<ICatalogService> _service = new();
        private readonly Mock<ILogger<FilesController>> _logger = new();

        private FilesController CreateController() => new(_service.Object, _logger.Object);

        [Fact]
        public async Task Get_ReturnsJsonFileResultWithBrands()
        {
            var brands = new List<CatalogBrand>
            {
                new() { Id = 1, Brand = "Azure" },
                new() { Id = 2, Brand = "Visual Studio" }
            };
            _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(brands);
            var controller = CreateController();

            var result = await controller.Get();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/json", file.ContentType);

            var dtos = JsonSerializer.Deserialize<List<FilesController.BrandDTO>>(file.FileContents);
            Assert.NotNull(dtos);
            Assert.Equal(2, dtos!.Count);
            Assert.Equal(1, dtos[0].Id);
            Assert.Equal("Azure", dtos[0].Brand);
            Assert.Equal(2, dtos[1].Id);
            Assert.Equal("Visual Studio", dtos[1].Brand);
            _service.Verify(s => s.GetCatalogBrandsAsync(), Times.Once);
        }

        [Fact]
        public async Task Get_NoBrands_ReturnsEmptyJsonArray()
        {
            _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());
            var controller = CreateController();

            var result = await controller.Get();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/json", file.ContentType);

            var dtos = JsonSerializer.Deserialize<List<FilesController.BrandDTO>>(file.FileContents);
            Assert.NotNull(dtos);
            Assert.Empty(dtos!);
        }
    }
}
