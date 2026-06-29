using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class BrandsControllerTests
    {
        private static readonly List<CatalogBrand> Brands = new()
        {
            new CatalogBrand { Id = 1, Brand = "Azure" },
            new CatalogBrand { Id = 2, Brand = ".NET" },
        };

        private static BrandsController CreateController(out Mock<ICatalogService> service)
        {
            service = new Mock<ICatalogService>();
            service.Setup(s => s.GetCatalogBrandsAsync())
                .ReturnsAsync(Brands);
            var logger = new Mock<ILogger<BrandsController>>();
            return new BrandsController(service.Object, logger.Object);
        }

        [Fact]
        public async Task Get_ReturnsAllBrands()
        {
            var controller = CreateController(out _);

            var result = await controller.Get();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var brands = Assert.IsAssignableFrom<IEnumerable<CatalogBrand>>(ok.Value);
            Assert.Equal(2, brands.Count());
        }

        [Fact]
        public async Task Get_ById_Existing_ReturnsBrand()
        {
            var controller = CreateController(out _);

            var result = await controller.Get(1);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var brand = Assert.IsType<CatalogBrand>(ok.Value);
            Assert.Equal("Azure", brand.Brand);
        }

        [Fact]
        public async Task Get_ById_Unknown_ReturnsNotFound()
        {
            var controller = CreateController(out _);

            var result = await controller.Get(999);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task Delete_Existing_ReturnsOk()
        {
            var controller = CreateController(out _);

            var result = await controller.Delete(2);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Delete_Unknown_ReturnsNotFound()
        {
            var controller = CreateController(out _);

            var result = await controller.Delete(999);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
