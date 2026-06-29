using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class CatalogControllerPaginationTests
    {
        private static CatalogController CreateController(Mock<ICatalogService> service)
        {
            return new CatalogController(
                service.Object,
                Mock.Of<IImageService>(),
                Mock.Of<ICatalogConfiguration>(),
                Mock.Of<ILogger<CatalogController>>());
        }

        [Fact]
        public async Task Edit_Post_RedirectsToIndex_PreservingPage()
        {
            var service = new Mock<ICatalogService>();
            service.Setup(s => s.UpdateCatalogItemAsync(It.IsAny<CatalogItem>()))
                .Returns(Task.CompletedTask);
            var controller = CreateController(service);
            var item = new CatalogItem { Id = 1, Name = "Test" };

            var result = await controller.Edit(item, pageSize: 10, pageIndex: 3);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
            Assert.Equal(3, redirect.RouteValues!["pageIndex"]);
            Assert.Equal(10, redirect.RouteValues!["pageSize"]);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_RedirectsToIndex_PreservingPage()
        {
            var service = new Mock<ICatalogService>();
            service.Setup(s => s.FindCatalogItemAsync(It.IsAny<int>()))
                .ReturnsAsync(new CatalogItem { Id = 1, Name = "Test" });
            service.Setup(s => s.RemoveCatalogItemAsync(It.IsAny<CatalogItem>()))
                .Returns(Task.CompletedTask);
            var controller = CreateController(service);

            var result = await controller.DeleteConfirmed(1, pageSize: 10, pageIndex: 3);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
            Assert.Equal(3, redirect.RouteValues!["pageIndex"]);
            Assert.Equal(10, redirect.RouteValues!["pageSize"]);
        }
    }
}
