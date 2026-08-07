using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShopCoreModernized.Tests
{
    public class BrandsControllerTests
    {
        private static BrandsController CreateController(FakeBrandService service) =>
            new BrandsController(service, NullLogger<BrandsController>.Instance);

        [Fact]
        public async Task Index_shows_every_brand()
        {
            var controller = CreateController(new FakeBrandService());

            var view = Assert.IsType<ViewResult>(await controller.Index());

            var model = Assert.IsAssignableFrom<IEnumerable<CatalogBrand>>(view.Model);
            Assert.Equal(2, model.Count());
        }

        [Fact]
        public async Task Details_returns_not_found_for_unknown_brand()
        {
            var controller = CreateController(new FakeBrandService());

            Assert.IsType<NotFoundResult>(await controller.Details(999));
        }

        [Fact]
        public async Task Create_redirects_to_index_and_persists_the_brand()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.Create(new CatalogBrand { Brand = "Contoso" });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BrandsController.Index), redirect.ActionName);
            Assert.Contains(service.Brands, b => b.Brand == "Contoso");
        }

        [Fact]
        public async Task Create_redisplays_the_form_when_the_model_is_invalid()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);
            controller.ModelState.AddModelError(nameof(CatalogBrand.Brand), "Required");

            Assert.IsType<ViewResult>(await controller.Create(new CatalogBrand()));
            Assert.Equal(2, service.Brands.Count);
        }

        [Fact]
        public async Task Edit_renames_the_existing_brand()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.Edit(new CatalogBrand { Id = 1, Brand = "Azure Cloud" });

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Azure Cloud", service.Brands.Single(b => b.Id == 1).Brand);
        }

        [Fact]
        public async Task DeleteConfirmed_removes_an_unreferenced_brand()
        {
            var service = new FakeBrandService();
            var controller = CreateController(service);

            var result = await controller.DeleteConfirmed(1);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.DoesNotContain(service.Brands, b => b.Id == 1);
        }

        [Fact]
        public async Task DeleteConfirmed_keeps_a_brand_that_catalog_items_still_reference()
        {
            var service = new FakeBrandService(inUse: new[] { 1 });
            var controller = CreateController(service);

            var result = await controller.DeleteConfirmed(1);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(service.Brands, b => b.Id == 1);
        }
    }
}
