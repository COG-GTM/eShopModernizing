using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class PicControllerTests : IDisposable
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<ILogger<PicController>> _logger = new();
    private readonly Mock<IWebHostEnvironment> _environment = new();
    private readonly string _webRoot;

    public PicControllerTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "eshop-pic-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "Pics"));
        _environment.SetupGet(e => e.WebRootPath).Returns(_webRoot);
        _environment.SetupGet(e => e.ContentRootPath).Returns(_webRoot);
    }

    private PicController CreateController() => new(_service.Object, _logger.Object, _environment.Object);

    [Fact]
    public async Task Index_ReturnsBadRequest_WhenIdNotPositive()
    {
        Assert.IsType<BadRequestResult>(await CreateController().Index(0));
    }

    [Fact]
    public async Task Index_ReturnsNotFound_WhenItemMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(1)).ReturnsAsync((CatalogItem?)null);

        Assert.IsType<NotFoundResult>(await CreateController().Index(1));
    }

    [Fact]
    public async Task Index_ReturnsNotFound_WhenFileMissing()
    {
        _service.Setup(s => s.FindCatalogItemAsync(1))
            .ReturnsAsync(new CatalogItem { Id = 1, PictureFileName = "missing.png" });

        Assert.IsType<NotFoundResult>(await CreateController().Index(1));
    }

    [Fact]
    public async Task Index_ReturnsFile_WhenFileExists()
    {
        var fileName = "1.png";
        await File.WriteAllBytesAsync(Path.Combine(_webRoot, "Pics", fileName), new byte[] { 1, 2, 3 });
        _service.Setup(s => s.FindCatalogItemAsync(1))
            .ReturnsAsync(new CatalogItem { Id = 1, PictureFileName = fileName });

        var result = await CreateController().Index(1);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(3, file.FileContents.Length);
    }

    [Fact]
    public async Task Index_ReturnsFile_WithOctetStream_ForUnknownExtension()
    {
        var fileName = "1.xyz";
        await File.WriteAllBytesAsync(Path.Combine(_webRoot, "Pics", fileName), new byte[] { 9 });
        _service.Setup(s => s.FindCatalogItemAsync(2))
            .ReturnsAsync(new CatalogItem { Id = 2, PictureFileName = fileName });

        var result = await CreateController().Index(2);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/octet-stream", file.ContentType);
    }

    public void Dispose()
    {
        try { Directory.Delete(_webRoot, recursive: true); } catch { /* best effort */ }
    }
}
