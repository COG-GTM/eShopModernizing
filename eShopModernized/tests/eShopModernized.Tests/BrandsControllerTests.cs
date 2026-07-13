using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class BrandsControllerTests
{
    private readonly Mock<ICatalogService> _service = new();

    private BrandsController CreateController() =>
        new(_service.Object, NullLogger<BrandsController>.Instance);

    private static List<CatalogBrand> SampleBrands() => new()
    {
        new CatalogBrand { Id = 1, Brand = "Azure" },
        new CatalogBrand { Id = 2, Brand = "Visual Studio" },
    };

    [Fact]
    public async Task Get_ReturnsOkWithAllBrands()
    {
        var brands = SampleBrands();
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(brands);

        var result = await CreateController().Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(brands, ok.Value);
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithBrand()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(SampleBrands());

        var result = await CreateController().Get(2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var brand = Assert.IsType<CatalogBrand>(ok.Value);
        Assert.Equal(2, brand.Id);
        Assert.Equal("Visual Studio", brand.Brand);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(SampleBrands());

        var result = await CreateController().Get(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(SampleBrands());

        var result = await CreateController().Delete(1);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Delete_WithUnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(SampleBrands());

        var result = await CreateController().Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
