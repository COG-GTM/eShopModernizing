using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Services;

public class ImageAzureStorageTests
{
    private static readonly Uri AccountUri = new("https://acct.blob.core.windows.net/");

    private readonly Mock<BlobServiceClient> _blobService = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<ICatalogConfiguration> _catalogConfiguration = new();
    private readonly Mock<ILogger<ImageAzureStorage>> _logger = new();

    public ImageAzureStorageTests()
    {
        _blobService.SetupGet(b => b.Uri).Returns(AccountUri);
    }

    private ImageAzureStorage CreateStorage() =>
        new(_blobService.Object, _configuration.Object, _catalogConfiguration.Object, _logger.Object);

    private static AsyncPageable<BlobItem> Pageable(params BlobItem[] items)
    {
        var page = Page<BlobItem>.FromValues(items, continuationToken: null, Mock.Of<Response>());
        return AsyncPageable<BlobItem>.FromPages(new[] { page });
    }

    private static IFormFile FormFile(string name, string contentType = "image/png")
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns(name);
        file.SetupGet(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 1, 2, 3 }));
        return file.Object;
    }

    [Fact]
    public void BaseUrl_ReturnsAccountUri()
    {
        Assert.Equal(AccountUri.ToString(), CreateStorage().BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_ReturnsDefaultPath()
    {
        Assert.Equal($"{AccountUri}pics/temp/default.png", CreateStorage().UrlDefaultImage());
    }

    [Fact]
    public void BuildUrlImage_ReturnsDefault_WhenNoPicture()
    {
        var item = new CatalogItem { Id = 1, PictureFileName = string.Empty };

        Assert.Equal($"{AccountUri}pics/temp/default.png", CreateStorage().BuildUrlImage(item));
    }

    [Fact]
    public void BuildUrlImage_ReturnsItemPath_WhenPicturePresent()
    {
        var item = new CatalogItem { Id = 9, PictureFileName = "9.png" };

        Assert.Equal($"{AccountUri}pics/9/9.png", CreateStorage().BuildUrlImage(item));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        CreateStorage().Dispose();
    }

    [Fact]
    public async Task UploadTempImageAsync_UploadsAndReturnsUri_WithCatalogItemId()
    {
        var blobUri = new Uri($"{AccountUri}pics/5/temp/photo.png");
        var blobClient = new Mock<BlobClient>();
        blobClient.SetupGet(b => b.Uri).Returns(blobUri);
        blobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, default, default, default), Mock.Of<Response>()));

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(default, default), Mock.Of<Response>()));
        container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClient.Object);

        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        var result = await CreateStorage().UploadTempImageAsync(FormFile("Photo.PNG"), 5);

        Assert.Equal(blobUri.ToString(), result);
        blobClient.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadTempImageAsync_UsesGuidPath_WhenNoCatalogItemId()
    {
        var blobClient = new Mock<BlobClient>();
        blobClient.SetupGet(b => b.Uri).Returns(new Uri($"{AccountUri}pics/temp/x/photo.png"));
        blobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, default, default, default), Mock.Of<Response>()));

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(default, default), Mock.Of<Response>()));
        container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClient.Object);

        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        var result = await CreateStorage().UploadTempImageAsync(FormFile("photo.png"), null);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task UploadTempImageAsync_LogsAndRethrows_OnError()
    {
        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Throws(new RequestFailedException("boom"));

        await Assert.ThrowsAsync<RequestFailedException>(() => CreateStorage().UploadTempImageAsync(FormFile("photo.png"), 1));
    }

    [Fact]
    public async Task UpdateImageAsync_ReturnsEarly_WhenNoTempImage()
    {
        var container = new Mock<BlobContainerClient>();
        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        await CreateStorage().UpdateImageAsync(new CatalogItem { Id = 1, TempImageName = null });

        container.Verify(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateImageAsync_CopiesTempToFinal_AndCleansUp()
    {
        var tempBlob = new Mock<BlobClient>();
        tempBlob.SetupGet(b => b.Uri).Returns(new Uri($"{AccountUri}pics/1/temp/new.png"));
        tempBlob.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var oldBlob = new Mock<BlobClient>();
        oldBlob.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var copyOperation = new Mock<CopyFromUriOperation>();
        copyOperation.Setup(o => o.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Response<long>>(Response.FromValue(1L, Mock.Of<Response>())));

        var imageBlob = new Mock<BlobClient>();
        imageBlob.Setup(b => b.StartCopyFromUriAsync(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(),
                It.IsAny<AccessTier?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<BlobRequestConditions>(),
                It.IsAny<RehydratePriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(copyOperation.Object);

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.GetBlobClient("1/temp/new.png")).Returns(tempBlob.Object);
        container.Setup(c => c.GetBlobClient("old/old.png")).Returns(oldBlob.Object);
        container.Setup(c => c.GetBlobClient("1/new.png")).Returns(imageBlob.Object);
        container.Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), "1/", It.IsAny<CancellationToken>()))
            .Returns(Pageable(BlobsModelFactory.BlobItem(name: "old/old.png")));

        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        await CreateStorage().UpdateImageAsync(new CatalogItem { Id = 1, TempImageName = "/pics/1/temp/new.png" });

        imageBlob.Verify(b => b.StartCopyFromUriAsync(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<AccessTier?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<BlobRequestConditions>(),
            It.IsAny<RehydratePriority?>(), It.IsAny<CancellationToken>()), Times.Once);
        tempBlob.Verify(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateImageAsync_LogsAndRethrows_OnError()
    {
        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Throws(new RequestFailedException("boom"));

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            CreateStorage().UpdateImageAsync(new CatalogItem { Id = 1, TempImageName = "/pics/1/temp/x.png" }));
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_CreatesContainer_ClearsAndSeedsImages()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var picsDir = Path.Combine(currentDir, "wwwroot", "Pics");
        Directory.CreateDirectory(picsDir);
        await File.WriteAllBytesAsync(Path.Combine(picsDir, "1.png"), new byte[] { 1 });
        await File.WriteAllBytesAsync(Path.Combine(picsDir, "default.png"), new byte[] { 2 });

        var uploadBlob = new Mock<BlobClient>();
        uploadBlob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, default, default, default), Mock.Of<Response>()));

        var existingBlob = new Mock<BlobClient>();
        existingBlob.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var container = new Mock<BlobContainerClient>();
        container.Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(default, default), Mock.Of<Response>()));
        container.Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), null, It.IsAny<CancellationToken>()))
            .Returns(Pageable(BlobsModelFactory.BlobItem(name: "stale.png")));
        container.Setup(c => c.GetBlobClient("stale.png")).Returns(existingBlob.Object);
        container.Setup(c => c.GetBlobClient(It.Is<string>(s => s != "stale.png"))).Returns(uploadBlob.Object);

        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Returns(container.Object);

        try
        {
            await CreateStorage().InitializeCatalogImagesAsync();
        }
        finally
        {
            try { File.Delete(Path.Combine(picsDir, "1.png")); } catch { }
            try { File.Delete(Path.Combine(picsDir, "default.png")); } catch { }
        }

        existingBlob.Verify(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        uploadBlob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task InitializeCatalogImagesAsync_LogsAndRethrows_OnError()
    {
        _blobService.Setup(b => b.GetBlobContainerClient("pics")).Throws(new RequestFailedException("boom"));

        await Assert.ThrowsAsync<RequestFailedException>(() => CreateStorage().InitializeCatalogImagesAsync());
    }
}
