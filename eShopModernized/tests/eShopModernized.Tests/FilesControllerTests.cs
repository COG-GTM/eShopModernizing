using System.Text;
using System.Text.Json;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class FilesControllerTests
{
    private readonly Mock<ICatalogService> _service = new();

    private FilesController CreateController() =>
        new(_service.Object, NullLogger<FilesController>.Instance);

    [Fact]
    public async Task Get_ReturnsJsonFileWithBrands()
    {
        var brands = new List<CatalogBrand>
        {
            new() { Id = 1, Brand = "Azure" },
            new() { Id = 2, Brand = "Visual Studio" },
        };
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(brands);

        var result = await CreateController().Get();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);

        var json = Encoding.UTF8.GetString(file.FileContents);
        var deserialized = JsonSerializer.Deserialize<List<FilesController.BrandDTO>>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("Azure", deserialized[0].Brand);
        Assert.Equal(2, deserialized[1].Id);
    }

    [Fact]
    public async Task Get_WithNoBrands_ReturnsEmptyJsonArrayFile()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>());

        var result = await CreateController().Get();

        var file = Assert.IsType<FileContentResult>(result);
        var json = Encoding.UTF8.GetString(file.FileContents);
        var deserialized = JsonSerializer.Deserialize<List<FilesController.BrandDTO>>(json);
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized!);
    }
}
