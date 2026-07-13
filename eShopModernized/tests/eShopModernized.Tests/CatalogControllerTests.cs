using eShopCoreModernized.Configuration;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class CatalogControllerTests
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<IImageService> _imageService = new();
    private readonly Mock<ICatalogConfiguration> _config = new();

    private CatalogController CreateController() =>
        new(_service.Object, _imageService.Object, _config.Object, NullLogger<CatalogController>.Instance);

    private static CatalogItem NewItem(int id, string name = "Item") =>
        new() { Id = id, Name = name, PictureFileName = "pic.png" };

    [Fact]
    public async Task Index_ReturnsViewWithPaginatedItems_AndBuildsImageUris()
    {
        var items = new[] { NewItem(1), NewItem(2) };
        var paginated = new PaginatedItemsViewModel<CatalogItem>(0, 10, 2, items);
        _service.Setup(s => s.GetCatalogItemsPaginatedAsync(10, 0)).ReturnsAsync(paginated);
        _imageService.Setup(i => i.BuildUrlImage(It.IsAny<CatalogItem>())).Returns("http://img/x.png");
        var controller = CreateController();

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(paginated, view.Model);
        _imageService.Verify(i => i.BuildUrlImage(It.IsAny<CatalogItem>()), Times.Exactly(2));
        Assert.All(items, i => Assert.Equal("http://img/x.png", i.PictureUri));
    }

    [Fact]
    public async Task Details_WithNullId_ReturnsBadRequest()
    {
        var result = await CreateController().Details(null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Details_WithUnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.FindCatalogItemAsync(99)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().Details(99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_WithValidId_ReturnsViewWithItemAndImageUri()
    {
        var item = NewItem(3);
        _service.Setup(s => s.FindCatalogItemAsync(3)).ReturnsAsync(item);
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("http://img/3.png");

        var result = await CreateController().Details(3);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(item, view.Model);
        Assert.Equal("http://img/3.png", item.PictureUri);
    }

    [Fact]
    public async Task Create_Get_ReturnsViewWithDefaultImageAndPopulatesViewBag()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());
        _service.Setup(s => s.GetCatalogTypesAsync()).ReturnsAsync(new List<CatalogType>());
        _imageService.Setup(i => i.UrlDefaultImage()).Returns("default.png");
        _config.SetupGet(c => c.UseAzureStorage).Returns(true);
        var controller = CreateController();

        var result = await controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CatalogItem>(view.Model);
        Assert.Equal("default.png", model.PictureUri);
        Assert.Equal(true, controller.ViewBag.UseAzureStorage);
        Assert.NotNull(controller.ViewBag.CatalogBrandId);
        Assert.NotNull(controller.ViewBag.CatalogTypeId);
    }

    [Fact]
    public async Task CreatePost_WithValidModel_CreatesItemAndRedirectsToIndex()
    {
        var item = NewItem(0, "New");
        var controller = CreateController();

        var result = await controller.Create(item);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
        _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
        _imageService.Verify(i => i.UpdateImageAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task CreatePost_WithTempImage_UpdatesImageAndSetsPictureFileName()
    {
        var item = NewItem(0, "New");
        item.TempImageName = "/tmp/uploaded.png";
        var controller = CreateController();

        var result = await controller.Create(item);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("uploaded.png", item.PictureFileName);
        _service.Verify(s => s.CreateCatalogItemAsync(item), Times.Once);
        _imageService.Verify(i => i.UpdateImageAsync(item), Times.Once);
    }

    [Fact]
    public async Task CreatePost_WithInvalidModel_ReturnsViewWithItem()
    {
        var item = NewItem(0, "New");
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());
        _service.Setup(s => s.GetCatalogTypesAsync()).ReturnsAsync(new List<CatalogType>());
        var controller = CreateController();
        controller.ModelState.AddModelError("Name", "Required");

        var result = await controller.Create(item);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(item, view.Model);
        _service.Verify(s => s.CreateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task Edit_Get_WithNullId_ReturnsBadRequest()
    {
        var result = await CreateController().Edit((int?)null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Edit_Get_WithUnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.FindCatalogItemAsync(42)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().Edit(42);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_WithValidId_ReturnsViewWithItem()
    {
        var item = NewItem(5);
        _service.Setup(s => s.FindCatalogItemAsync(5)).ReturnsAsync(item);
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());
        _service.Setup(s => s.GetCatalogTypesAsync()).ReturnsAsync(new List<CatalogType>());
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("http://img/5.png");

        var result = await CreateController().Edit(5);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(item, view.Model);
    }

    [Fact]
    public async Task EditPost_WithValidModel_UpdatesItemAndRedirectsToIndex()
    {
        var item = NewItem(6, "Edited");
        var controller = CreateController();

        var result = await controller.Edit(item);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
        _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
        _imageService.Verify(i => i.UpdateImageAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task EditPost_WithTempImage_UpdatesImageAndSetsPictureFileName()
    {
        var item = NewItem(7, "Edited");
        item.TempImageName = "/tmp/new.png";
        var controller = CreateController();

        var result = await controller.Edit(item);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("new.png", item.PictureFileName);
        _imageService.Verify(i => i.UpdateImageAsync(item), Times.Once);
        _service.Verify(s => s.UpdateCatalogItemAsync(item), Times.Once);
    }

    [Fact]
    public async Task EditPost_WithInvalidModel_ReturnsViewWithoutUpdating()
    {
        var item = NewItem(8, "Edited");
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());
        _service.Setup(s => s.GetCatalogTypesAsync()).ReturnsAsync(new List<CatalogType>());
        var controller = CreateController();
        controller.ModelState.AddModelError("Name", "Required");

        var result = await controller.Edit(item);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(item, view.Model);
        _service.Verify(s => s.UpdateCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Get_WithNullId_ReturnsBadRequest()
    {
        var result = await CreateController().Delete((int?)null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Delete_Get_WithUnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.FindCatalogItemAsync(11)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().Delete(11);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_Get_WithValidId_ReturnsViewWithItem()
    {
        var item = NewItem(12);
        _service.Setup(s => s.FindCatalogItemAsync(12)).ReturnsAsync(item);
        _imageService.Setup(i => i.BuildUrlImage(item)).Returns("http://img/12.png");

        var result = await CreateController().Delete(12);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(item, view.Model);
    }

    [Fact]
    public async Task DeleteConfirmed_WithExistingItem_RemovesAndRedirectsToIndex()
    {
        var item = NewItem(13);
        _service.Setup(s => s.FindCatalogItemAsync(13)).ReturnsAsync(item);

        var result = await CreateController().DeleteConfirmed(13);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(CatalogController.Index), redirect.ActionName);
        _service.Verify(s => s.RemoveCatalogItemAsync(item), Times.Once);
    }

    [Fact]
    public async Task DeleteConfirmed_WithMissingItem_RedirectsWithoutRemoving()
    {
        _service.Setup(s => s.FindCatalogItemAsync(14)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().DeleteConfirmed(14);

        Assert.IsType<RedirectToActionResult>(result);
        _service.Verify(s => s.RemoveCatalogItemAsync(It.IsAny<CatalogItem>()), Times.Never);
    }
}
