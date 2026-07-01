using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.ViewModel;
using eShopModernized.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class CatalogControllerTests
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<IImageService> _imageService = new();
    private readonly Mock<ICatalogConfiguration> _config = new();
    private readonly Mock<ILogger<CatalogController>> _logger = new();

    private CatalogController CreateController()
    {
        var controller = new CatalogController(_service.Object, _imageService.Object, _config.Object, _logger.Object);
        MvcTestContext.Configure(controller);
        return controller;
    }

    private void SetupLookups()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(TestData.Brands());
        _service.Setup(s => s.GetCatalogTypesAsync()).ReturnsAsync(TestData.Types());
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);
    }

    [Fact]
    public async Task Index_ReturnsView_AndSetsPictureUri()
    {
        var items = TestData.Items();
        _service.Setup(s => s.GetCatalogItemsPaginatedAsync(10, 0))
            .ReturnsAsync(new PaginatedItemsViewModel<CatalogItem>(0, 10, items.Count, items));
        _imageService.Setup(i => i.BuildUrlImage(It.IsAny<CatalogItem>())).Returns("/pics/x.png");

        var result = await CreateController().Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<PaginatedItemsViewModel<CatalogItem>>(view.Model);
        _imageService.Verify(i => i.BuildUrlImage(It.IsAny<CatalogItem>()), Times.Exactly(items.Count));
    }

    [Fact]
    public async Task Details_ReturnsBadRequest_WhenIdNull()
    {
        Assert.IsType<BadRequestResult>(await CreateController().Details(null));
    }

    [Fact]
    public async Task Details_ReturnsNotFound_WhenMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync((CatalogItem?)null);

        Assert.IsType<NotFoundResult>(await CreateController().Details(5));
    }

    [Fact]
    public async Task Details_ReturnsView_WhenFound()
    {
        var item = TestData.Items().First();
        _service.Setup(s => s.FindCatalogItemAsync(item.Id)).ReturnsAsync(item);
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("/pics/1.png");

        var view = Assert.IsType<ViewResult>(await CreateController().Details(item.Id));
        Assert.Same(item, view.Model);
        Assert.Equal("/pics/1.png", item.PictureUri);
    }

    [Fact]
    public async Task Create_Get_ReturnsView_WithDefaultImage()
    {
        SetupLookups();
        _imageService.Setup(i => i.UrlDefaultImage()).Returns("/pics/default.png");

        var view = Assert.IsType<ViewResult>(await CreateController().Create());
        var model = Assert.IsType<CatalogItem>(view.Model);
        Assert.Equal("/pics/default.png", model.PictureUri);
    }

    [Fact]
    public async Task Create_Post_Valid_WithTempImage_CreatesAndUpdatesImage()
    {
        var controller = CreateController();
        var item = new CatalogItem { Name = "New", TempImageName = "/pics/temp/abc.png" };

        var result = await controller.Create(item);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("abc.png", item.PictureFileName);
        _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
        _imageService.Verify(i => i.UpdateImageAsync(item), Times.Once);
    }

    [Fact]
    public async Task Create_Post_Valid_WithoutTempImage_DoesNotUpdateImage()
    {
        var controller = CreateController();
        var item = new CatalogItem { Name = "New" };

        var result = await controller.Create(item);

        Assert.IsType<RedirectToActionResult>(result);
        _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
        _imageService.Verify(i => i.UpdateImageAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task Create_Post_Invalid_ReturnsView()
    {
        SetupLookups();
        var controller = CreateController();
        controller.ModelState.AddModelError("Name", "Required");
        var item = new CatalogItem();

        var view = Assert.IsType<ViewResult>(await controller.Create(item));
        Assert.Same(item, view.Model);
        _service.Verify(s => s.CreateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task Edit_Get_ReturnsBadRequest_WhenIdNull()
    {
        Assert.IsType<BadRequestResult>(await CreateController().Edit((int?)null));
    }

    [Fact]
    public async Task Edit_Get_ReturnsNotFound_WhenMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(9)).ReturnsAsync((CatalogItem?)null);
        Assert.IsType<NotFoundResult>(await CreateController().Edit(9));
    }

    [Fact]
    public async Task Edit_Get_ReturnsView_WhenFound()
    {
        SetupLookups();
        var item = TestData.Items().First();
        _service.Setup(s => s.FindCatalogItemAsync(item.Id)).ReturnsAsync(item);
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("/pics/1.png");

        var view = Assert.IsType<ViewResult>(await CreateController().Edit(item.Id));
        Assert.Same(item, view.Model);
    }

    [Fact]
    public async Task Edit_Post_Valid_WithTempImage_UpdatesImageAndItem()
    {
        var controller = CreateController();
        var item = new CatalogItem { Id = 1, Name = "Edited", TempImageName = "/pics/temp/new.png" };

        var result = await controller.Edit(item);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("new.png", item.PictureFileName);
        _imageService.Verify(i => i.UpdateImageAsync(item), Times.Once);
        _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
    }

    [Fact]
    public async Task Edit_Post_Valid_WithoutTempImage_UpdatesItemOnly()
    {
        var controller = CreateController();
        var item = new CatalogItem { Id = 1, Name = "Edited" };

        var result = await controller.Edit(item);

        Assert.IsType<RedirectToActionResult>(result);
        _imageService.Verify(i => i.UpdateImageAsync(It.IsAny<CatalogItem>()), Times.Never);
        _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
    }

    [Fact]
    public async Task Edit_Post_Invalid_ReturnsView()
    {
        SetupLookups();
        var controller = CreateController();
        controller.ModelState.AddModelError("Name", "Required");
        var item = new CatalogItem { Id = 1 };

        var view = Assert.IsType<ViewResult>(await controller.Edit(item));
        Assert.Same(item, view.Model);
        _service.Verify(s => s.UpdateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Get_ReturnsBadRequest_WhenIdNull()
    {
        Assert.IsType<BadRequestResult>(await CreateController().Delete((int?)null));
    }

    [Fact]
    public async Task Delete_Get_ReturnsNotFound_WhenMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(3)).ReturnsAsync((CatalogItem?)null);
        Assert.IsType<NotFoundResult>(await CreateController().Delete(3));
    }

    [Fact]
    public async Task Delete_Get_ReturnsView_WhenFound()
    {
        var item = TestData.Items().First();
        _service.Setup(s => s.FindCatalogItemAsync(item.Id)).ReturnsAsync(item);
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("/pics/1.png");

        var view = Assert.IsType<ViewResult>(await CreateController().Delete(item.Id));
        Assert.Same(item, view.Model);
    }

    [Fact]
    public async Task DeleteConfirmed_RemovesItem_WhenFound()
    {
        var item = TestData.Items().First();
        _service.Setup(s => s.FindCatalogItemAsync(item.Id)).ReturnsAsync(item);

        var result = await CreateController().DeleteConfirmed(item.Id);

        Assert.IsType<RedirectToActionResult>(result);
        _service.Verify(s => s.RemoveCatalogItemAsync(item), Times.Once);
    }

    [Fact]
    public async Task DeleteConfirmed_DoesNothing_WhenMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(50)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().DeleteConfirmed(50);

        Assert.IsType<RedirectToActionResult>(result);
        _service.Verify(s => s.RemoveCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }
}
