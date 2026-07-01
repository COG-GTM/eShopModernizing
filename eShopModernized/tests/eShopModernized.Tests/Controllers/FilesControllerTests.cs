using System.Text.Json;
using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<ILogger<FilesController>> _logger = new();

    [Fact]
    public async Task Get_ReturnsJsonFile_WithSerializedBrands()
    {
        _service.Setup(s => s.GetCatalogBrandsAsync()).ReturnsAsync(new List<CatalogBrand>
        {
            new() { Id = 1, Brand = "Azure" },
            new() { Id = 2, Brand = ".NET" }
        });

        var controller = new FilesController(_service.Object, _logger.Object);

        var result = await controller.Get();

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", fileResult.ContentType);

        var dtos = JsonSerializer.Deserialize<List<FilesController.BrandDTO>>(fileResult.FileContents);
        Assert.NotNull(dtos);
        Assert.Equal(2, dtos!.Count);
        Assert.Equal("Azure", dtos[0].Brand);
    }

    [Fact]
    public void BrandDTO_ExposesIdAndBrand()
    {
        var dto = new FilesController.BrandDTO { Id = 7, Brand = "Contoso" };

        Assert.Equal(7, dto.Id);
        Assert.Equal("Contoso", dto.Brand);
    }
}
