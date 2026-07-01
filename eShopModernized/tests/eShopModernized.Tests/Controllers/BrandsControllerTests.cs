using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class BrandsControllerTests
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<ILogger<BrandsController>> _logger = new();

    private BrandsController CreateController() => new(_service.Object, _logger.Object);

    private static List<CatalogBrand> Brands() => new()
    {
        new CatalogBrand { Id = 1, Brand = "Azure" },
        new CatalogBrand { Id = 2, Brand = ".NET" }
    };

    [Fact]
    public async Task Get_ReturnsOkWithAllBrands()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(Brands());

        var result = await CreateController().Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var brands = Assert.IsAssignableFrom<IEnumerable<CatalogBrand>>(ok.Value);
        Assert.Equal(2, brands.Count());
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenBrandExists()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(Brands());

        var result = await CreateController().Get(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var brand = Assert.IsType<CatalogBrand>(ok.Value);
        Assert.Equal(1, brand.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenBrandMissing()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(Brands());

        var result = await CreateController().Get(99);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenBrandExists()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(Brands());

        var result = await CreateController().Delete(2);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenBrandMissing()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(Brands());

        var result = await CreateController().Delete(42);

        Assert.IsType<NotFoundResult>(result);
    }
}
