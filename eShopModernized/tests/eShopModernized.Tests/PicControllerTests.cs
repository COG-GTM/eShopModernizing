using eShopCoreModernized.Controllers;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eShopModernized.Tests;

public class PicControllerTests : IDisposable
{
    private readonly Mock<ICatalogService> _service = new();
    private readonly Mock<IWebHostEnvironment> _environment = new();
    private readonly string _contentRoot;

    public PicControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "pic-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);
        _environment.SetupGet(e => e.WebRootPath).Returns(_contentRoot);
        _environment.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
    }

    private PicController CreateController() =>
        new(_service.Object, NullLogger<PicController>.Instance, _environment.Object);

    private string CreatePicFile(string fileName, byte[] content)
    {
        var picsDir = Path.Combine(_contentRoot, "Pics");
        Directory.CreateDirectory(picsDir);
        var path = Path.Combine(picsDir, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Index_WithNonPositiveId_ReturnsBadRequest(int id)
    {
        var result = await CreateController().Index(id);

        Assert.IsType<BadRequestResult>(result);
        _service.Verify(s => s.FindCatalogItemAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Index_WithUnknownItem_ReturnsNotFound()
    {
        _service.Setup(s => s.FindCatalogItemAsync(7)).ReturnsAsync((CatalogItem?)null);

        var result = await CreateController().Index(7);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_WhenPictureFileMissingOnDisk_ReturnsNotFound()
    {
        _service.Setup(s => s.FindCatalogItemAsync(8))
            .ReturnsAsync(new CatalogItem { Id = 8, Name = "X", PictureFileName = "missing.png" });

        var result = await CreateController().Index(8);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_WithExistingPictureFile_ReturnsFileWithCorrectMimeType()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        CreatePicFile("item9.png", bytes);
        _service.Setup(s => s.FindCatalogItemAsync(9))
            .ReturnsAsync(new CatalogItem { Id = 9, Name = "X", PictureFileName = "item9.png" });

        var result = await CreateController().Index(9);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(bytes, file.FileContents);
    }

    [Fact]
    public async Task Index_WithJpegPictureFile_ReturnsJpegMimeType()
    {
        CreatePicFile("item10.jpg", new byte[] { 9 });
        _service.Setup(s => s.FindCatalogItemAsync(10))
            .ReturnsAsync(new CatalogItem { Id = 10, Name = "X", PictureFileName = "item10.jpg" });

        var result = await CreateController().Index(10);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
