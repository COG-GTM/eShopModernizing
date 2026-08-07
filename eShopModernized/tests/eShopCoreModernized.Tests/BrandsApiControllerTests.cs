using eShopCoreModernized.Controllers.Api;
using eShopCoreModernized.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShopCoreModernized.Tests
{
    public class BrandsApiControllerTests
    {
        private static BrandsApiController CreateController(FakeBrandService service) =>
            new BrandsApiController(service, NullLogger<BrandsApiController>.Instance);

        [Fact]
        public async Task Get_returns_every_brand()
        {
            var controller = CreateController(new FakeBrandService());

            var result = await controller.Get();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<CatalogBrand>>(ok.Value).Count());
        }

        [Fact]
        public async Task Get_by_id_returns_not_found_for_unknown_brand()
        {
            var controller = CreateController(new FakeBrandService());

            Assert.IsType<NotFoundResult>((await controller.Get(999)).Result);
        }

        [Fact]
        public async Task Post_creates_the_brand_and_returns_its_location()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.Post(new CatalogBrand { Brand = "Contoso" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(3, Assert.IsType<CatalogBrand>(created.Value).Id);
            Assert.Contains(service.Brands, b => b.Brand == "Contoso");
        }

        [Fact]
        public async Task Put_renames_the_brand()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.Put(1, new CatalogBrand { Id = 1, Brand = "Azure Cloud" });

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Azure Cloud", service.Brands.Single(b => b.Id == 1).Brand);
        }

        [Fact]
        public async Task Put_returns_not_found_for_unknown_brand()
        {
            var controller = CreateController(new FakeBrandService());

            Assert.IsType<NotFoundResult>(await controller.Put(999, new CatalogBrand { Brand = "Nope" }));
        }

        [Fact]
        public async Task Delete_removes_an_unreferenced_brand()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.Delete(1);

            Assert.IsType<NoContentResult>(result);
            Assert.DoesNotContain(service.Brands, b => b.Id == 1);
        }

        [Fact]
        public async Task Delete_conflicts_when_catalog_items_still_reference_the_brand()
        {
            var service = new FakeBrandService(inUse: new[] { 1 });
            var controller = CreateController(service);

            var result = await controller.Delete(1);

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains(service.Brands, b => b.Id == 1);
        }
    }
}
