using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class CatalogControllerTests
    {
        private readonly Mock<ICatalogService> _service = new();
        private readonly Mock<IImageService> _imageService = new();
        private readonly Mock<ICatalogConfiguration> _configuration = new();

        private CatalogController CreateController()
        {
            _imageService.Setup(s => s.BuildUrlImage(It.IsAny<CatalogItem>())).Returns("/pics/x.png");
            var logger = new Mock<ILogger<CatalogController>>();
            return new CatalogController(_service.Object, _imageService.Object, _configuration.Object, logger.Object);
        }

        [Fact]
        public async Task Index_ReturnsViewWithPaginatedItems()
        {
            var items = new List<CatalogItem> { new() { Id = 1, Name = "Item" } };
            var paginated = new PaginatedItemsViewModel<CatalogItem>(0, 10, 1, items);
            _service.Setup(s => s.GetCatalogItemsPaginatedAsync(10, 0)).ReturnsAsync(paginated);
            var controller = CreateController();

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(paginated, view.Model);
            _imageService.Verify(s => s.BuildUrlImage(It.IsAny<CatalogItem>()), Times.Once);
        }

        [Fact]
        public async Task Details_NullId_ReturnsBadRequest()
        {
            var controller = CreateController();

            var result = await controller.Details(null);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Details_UnknownId_ReturnsNotFound()
        {
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync((CatalogItem?)null);
            var controller = CreateController();

            var result = await controller.Details(5);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_ExistingId_ReturnsViewWithItem()
        {
            var item = new CatalogItem { Id = 5, Name = "Found" };
            _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Details(5);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(item, view.Model);
            Assert.Equal("/pics/x.png", item.PictureUri);
        }

        [Fact]
        public async Task DeleteConfirmed_ExistingItem_RemovesAndRedirects()
        {
            var item = new CatalogItem { Id = 7 };
            _service.Setup(s => s.FindCatalogItemAsync(7)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.DeleteConfirmed(7);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
            _service.Verify(s => s.RemoveCatalogItemAsync(item), Times.Once);
        }

        [Fact]
        public async Task DeleteConfirmed_UnknownItem_RedirectsWithoutRemoving()
        {
            _service.Setup(s => s.FindCatalogItemAsync(7)).ReturnsAsync((CatalogItem?)null);
            var controller = CreateController();

            var result = await controller.DeleteConfirmed(7);

            Assert.IsType<RedirectToActionResult>(result);
            _service.Verify(s => s.RemoveCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_CreatesItemAndRedirects()
        {
            var controller = CreateController();
            var item = new CatalogItem { Name = "New" };

            var result = await controller.Create(item);

            Assert.IsType<RedirectToActionResult>(result);
            _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
        }

        private void SetupBrandsAndTypes()
        {
            _service.Setup(s => s.GetCatalogBrandsAsync())
                .ReturnsAsync(new List<CatalogBrand> { new() { Id = 1, Brand = "Brand1" } });
            _service.Setup(s => s.GetCatalogTypesAsync())
                .ReturnsAsync(new List<CatalogType> { new() { Id = 2, Type = "Type1" } });
        }

        [Fact]
        public async Task Create_Get_ReturnsViewWithDefaultsAndViewBag()
        {
            SetupBrandsAndTypes();
            _configuration.Setup(c => c.UseAzureStorage).Returns(true);
            _imageService.Setup(s => s.UrlDefaultImage()).Returns("/pics/default.png");
            var controller = CreateController();

            var result = await controller.Create();

            var view = Assert.IsType<ViewResult>(result);
            var item = Assert.IsType<CatalogItem>(view.Model);
            Assert.Equal("/pics/default.png", item.PictureUri);
            Assert.NotNull(controller.ViewBag.CatalogBrandId);
            Assert.NotNull(controller.ViewBag.CatalogTypeId);
            Assert.True((bool)controller.ViewBag.UseAzureStorage);
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithoutCreating()
        {
            SetupBrandsAndTypes();
            var controller = CreateController();
            controller.ModelState.AddModelError("Name", "required");
            var item = new CatalogItem { CatalogBrandId = 1, CatalogTypeId = 2 };

            var result = await controller.Create(item);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(item, view.Model);
            _service.Verify(s => s.CreateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModelWithTempImage_SetsFileNameAndUpdatesImage()
        {
            var controller = CreateController();
            var item = new CatalogItem { Name = "New", TempImageName = "/tmp/uploads/photo.png" };

            var result = await controller.Create(item);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("photo.png", item.PictureFileName);
            _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
            _imageService.Verify(s => s.UpdateImageAsync(item), Times.Once);
        }

        [Fact]
        public async Task Edit_Get_NullId_ReturnsBadRequest()
        {
            var controller = CreateController();

            var result = await controller.Edit((int?)null);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Get_UnknownId_ReturnsNotFound()
        {
            _service.Setup(s => s.FindCatalogItemAsync(9)).ReturnsAsync((CatalogItem?)null);
            var controller = CreateController();

            var result = await controller.Edit(9);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_ExistingId_ReturnsViewWithViewBag()
        {
            SetupBrandsAndTypes();
            _configuration.Setup(c => c.UseAzureStorage).Returns(false);
            var item = new CatalogItem { Id = 3, Name = "Existing", CatalogBrandId = 1, CatalogTypeId = 2 };
            _service.Setup(s => s.FindCatalogItemAsync(3)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Edit(3);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(item, view.Model);
            Assert.Equal("/pics/x.png", item.PictureUri);
            Assert.NotNull(controller.ViewBag.CatalogBrandId);
            Assert.NotNull(controller.ViewBag.CatalogTypeId);
            Assert.False((bool)controller.ViewBag.UseAzureStorage);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_UpdatesAndRedirects()
        {
            var controller = CreateController();
            var item = new CatalogItem { Id = 3, Name = "Updated" };

            var result = await controller.Edit(item);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
            _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
            _imageService.Verify(s => s.UpdateImageAsync(It.IsAny<CatalogItem>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModelWithTempImage_UpdatesImageAndItem()
        {
            var controller = CreateController();
            var item = new CatalogItem { Id = 3, Name = "Updated", TempImageName = "/tmp/uploads/edit.png" };

            var result = await controller.Edit(item);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("edit.png", item.PictureFileName);
            _imageService.Verify(s => s.UpdateImageAsync(item), Times.Once);
            _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithoutUpdating()
        {
            SetupBrandsAndTypes();
            var controller = CreateController();
            controller.ModelState.AddModelError("Name", "required");
            var item = new CatalogItem { Id = 3, CatalogBrandId = 1, CatalogTypeId = 2 };

            var result = await controller.Edit(item);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(item, view.Model);
            _service.Verify(s => s.UpdateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
        }

        [Fact]
        public async Task Delete_Get_NullId_ReturnsBadRequest()
        {
            var controller = CreateController();

            var result = await controller.Delete(null);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Delete_Get_UnknownId_ReturnsNotFound()
        {
            _service.Setup(s => s.FindCatalogItemAsync(11)).ReturnsAsync((CatalogItem?)null);
            var controller = CreateController();

            var result = await controller.Delete(11);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_Get_ExistingId_ReturnsViewWithItem()
        {
            var item = new CatalogItem { Id = 11, Name = "ToDelete" };
            _service.Setup(s => s.FindCatalogItemAsync(11)).ReturnsAsync(item);
            var controller = CreateController();

            var result = await controller.Delete(11);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(item, view.Model);
            Assert.Equal("/pics/x.png", item.PictureUri);
        }
    }
}
