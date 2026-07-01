using eShopCoreAPI.Controllers;
using eShopCoreAPI.Models;
using eShopCoreAPI.Services;
using eShopCoreAPI.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace eShopCoreAPI.Tests;

public class CatalogControllerTests
{
    private readonly Mock<ICatalogService> _serviceMock = new();
    private readonly CatalogController _controller;

    public CatalogControllerTests()
    {
        _controller = new CatalogController(_serviceMock.Object, NullLogger<CatalogController>.Instance);
    }

    [Fact]
    public void GetCatalogItems_ReturnsOkWithPaginatedItems()
    {
        var expected = new PaginatedItemsViewModel<CatalogItem>
        {
            PageIndex = 0,
            PageSize = 10,
            Count = 1,
            Data = new List<CatalogItem> { new() { Id = 1, Name = "Mug" } }
        };
        _serviceMock.Setup(s => s.GetCatalogItemsPaginated(10, 0)).Returns(expected);

        var result = _controller.GetCatalogItems();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public void GetCatalogItem_WhenFound_ReturnsOk()
    {
        var item = new CatalogItem { Id = 42, Name = "Hoodie" };
        _serviceMock.Setup(s => s.FindCatalogItem(42)).Returns(item);

        var result = _controller.GetCatalogItem(42);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(item, ok.Value);
    }

    [Fact]
    public void GetCatalogItem_WhenMissing_ReturnsNotFound()
    {
        _serviceMock.Setup(s => s.FindCatalogItem(999)).Returns((CatalogItem?)null);

        var result = _controller.GetCatalogItem(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetCatalogBrands_ReturnsOk()
    {
        var brands = new List<CatalogBrand> { new() { Id = 1, Brand = "Azure" } };
        _serviceMock.Setup(s => s.GetCatalogBrands()).Returns(brands);

        var result = _controller.GetCatalogBrands();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(brands, ok.Value);
    }

    [Fact]
    public void GetCatalogTypes_ReturnsOk()
    {
        var types = new List<CatalogType> { new() { Id = 1, Type = "Mug" } };
        _serviceMock.Setup(s => s.GetCatalogTypes()).Returns(types);

        var result = _controller.GetCatalogTypes();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(types, ok.Value);
    }

    [Fact]
    public void CreateCatalogItem_ReturnsCreatedAtAction()
    {
        var item = new CatalogItem { Id = 7, Name = "Sheet", CatalogBrandId = 1, CatalogTypeId = 1 };

        var result = _controller.CreateCatalogItem(item);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(CatalogController.GetCatalogItem), created.ActionName);
        _serviceMock.Verify(s => s.CreateCatalogItem(item), Times.Once);
    }

    [Fact]
    public void UpdateCatalogItem_WhenIdMismatch_ReturnsBadRequest()
    {
        var result = _controller.UpdateCatalogItem(1, new CatalogItem { Id = 2, Name = "X" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ProblemDetails>(badRequest.Value);
        _serviceMock.Verify(s => s.UpdateCatalogItem(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public void UpdateCatalogItem_WhenMissing_ReturnsNotFound()
    {
        _serviceMock.Setup(s => s.FindCatalogItem(5)).Returns((CatalogItem?)null);

        var result = _controller.UpdateCatalogItem(5, new CatalogItem { Id = 5, Name = "X" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void UpdateCatalogItem_WhenFound_ReturnsNoContent()
    {
        var item = new CatalogItem { Id = 5, Name = "X" };
        _serviceMock.Setup(s => s.FindCatalogItem(5)).Returns(item);

        var result = _controller.UpdateCatalogItem(5, item);

        Assert.IsType<NoContentResult>(result);
        _serviceMock.Verify(s => s.UpdateCatalogItem(item), Times.Once);
    }

    [Fact]
    public void DeleteCatalogItem_WhenMissing_ReturnsNotFound()
    {
        _serviceMock.Setup(s => s.FindCatalogItem(9)).Returns((CatalogItem?)null);

        var result = _controller.DeleteCatalogItem(9);

        Assert.IsType<NotFoundResult>(result);
        _serviceMock.Verify(s => s.RemoveCatalogItem(It.IsAny<CatalogItem>()), Times.Never);
    }

    [Fact]
    public void DeleteCatalogItem_WhenFound_ReturnsNoContent()
    {
        var item = new CatalogItem { Id = 9, Name = "X" };
        _serviceMock.Setup(s => s.FindCatalogItem(9)).Returns(item);

        var result = _controller.DeleteCatalogItem(9);

        Assert.IsType<NoContentResult>(result);
        _serviceMock.Verify(s => s.RemoveCatalogItem(item), Times.Once);
    }
}
