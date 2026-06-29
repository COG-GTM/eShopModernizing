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
    }
}
