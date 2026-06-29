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

namespace eShopModernized.Tests.Services
{
    public class ImageAzureStorageTests
    {
        private const string AccountUri = "https://account.blob.core.windows.net/";

        private readonly Mock<BlobServiceClient> _blobServiceClient = new();
        private readonly Mock<BlobContainerClient> _containerClient = new();
        private readonly Mock<IConfiguration> _configuration = new();
        private readonly Mock<ICatalogConfiguration> _catalogConfiguration = new();
        private readonly Mock<ILogger<ImageAzureStorage>> _logger = new();

        public ImageAzureStorageTests()
        {
            _blobServiceClient.SetupGet(c => c.Uri).Returns(new Uri(AccountUri));
            _blobServiceClient
                .Setup(c => c.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(_containerClient.Object);
        }

        private ImageAzureStorage CreateStorage() => new ImageAzureStorage(
            _blobServiceClient.Object,
            _configuration.Object,
            _catalogConfiguration.Object,
            _logger.Object);

        private static AsyncPageable<BlobItem> ToAsyncPageable(params BlobItem[] items)
        {
            var page = Page<BlobItem>.FromValues(items.ToList(), continuationToken: null, response: Mock.Of<Response>());
            return AsyncPageable<BlobItem>.FromPages(new[] { page });
        }

        private Mock<BlobClient> SetupBlobClient(string? uri = null)
        {
            var blobClient = new Mock<BlobClient>();
            blobClient.SetupGet(b => b.Uri).Returns(new Uri(uri ?? AccountUri + "pics/blob"));
            blobClient
                .Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            return blobClient;
        }

        private void SetupCreateIfNotExists()
        {
            _containerClient
                .Setup(c => c.CreateIfNotExistsAsync(
                    It.IsAny<PublicAccessType>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobContainerEncryptionScopeOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobContainerInfo(default, default),
                    Mock.Of<Response>()));
        }

        // --- Simple synchronous methods ---

        [Fact]
        public void BaseUrl_ReturnsBlobServiceUri()
        {
            Assert.Equal(AccountUri, CreateStorage().BaseUrl());
        }

        [Fact]
        public void UrlDefaultImage_ReturnsDefaultPicsPath()
        {
            Assert.Equal($"{AccountUri}pics/temp/default.png", CreateStorage().UrlDefaultImage());
        }

        [Fact]
        public void BuildUrlImage_WithPictureFileName_ReturnsItemScopedUrl()
        {
            var item = new CatalogItem { Id = 7, PictureFileName = "7.png" };

            Assert.Equal($"{AccountUri}pics/7/7.png", CreateStorage().BuildUrlImage(item));
        }

        [Fact]
        public void BuildUrlImage_WithoutPictureFileName_ReturnsDefaultImage()
        {
            var item = new CatalogItem { Id = 7, PictureFileName = string.Empty };

            Assert.Equal($"{AccountUri}pics/temp/default.png", CreateStorage().BuildUrlImage(item));
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            Assert.Null(Record.Exception(() => CreateStorage().Dispose()));
        }

        // --- InitializeCatalogImagesAsync ---

        [Fact]
        public async Task InitializeCatalogImagesAsync_CreatesContainerAndEnumeratesBlobs()
        {
            SetupCreateIfNotExists();
            var existingBlob = SetupBlobClient();
            _containerClient
                .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(ToAsyncPageable(BlobsModelFactory.BlobItem(name: "old.png")));
            _containerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(existingBlob.Object);

            var exception = await Record.ExceptionAsync(() => CreateStorage().InitializeCatalogImagesAsync());

            Assert.Null(exception);
            _containerClient.Verify(c => c.CreateIfNotExistsAsync(
                It.IsAny<PublicAccessType>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            existingBlob.Verify(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeCatalogImagesAsync_WhenContainerCreationFails_LogsAndThrows()
        {
            _containerClient
                .Setup(c => c.CreateIfNotExistsAsync(
                    It.IsAny<PublicAccessType>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobContainerEncryptionScopeOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStorage().InitializeCatalogImagesAsync());

            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // --- UpdateImageAsync ---

        [Fact]
        public async Task UpdateImageAsync_WithEmptyTempImageName_ReturnsEarly()
        {
            var item = new CatalogItem { Id = 1, TempImageName = string.Empty };

            var exception = await Record.ExceptionAsync(() => CreateStorage().UpdateImageAsync(item));

            Assert.Null(exception);
            _containerClient.Verify(
                c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateImageAsync_CopiesTempBlobAndDeletesExisting()
        {
            var item = new CatalogItem { Id = 1, TempImageName = "/pics/1/temp/new.png" };

            _containerClient
                .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(ToAsyncPageable(BlobsModelFactory.BlobItem(name: "1/old.png")));

            var tempBlob = SetupBlobClient(AccountUri + "pics/1/temp/new.png");
            var deleteBlob = SetupBlobClient();
            var imageBlob = SetupBlobClient(AccountUri + "pics/1/new.png");

            var copyOperation = new Mock<CopyFromUriOperation>();
            copyOperation
                .Setup(o => o.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(0L, Mock.Of<Response>()));

            imageBlob
                .Setup(b => b.StartCopyFromUriAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<RehydratePriority?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(copyOperation.Object);

            _containerClient
                .Setup(c => c.GetBlobClient(It.Is<string>(s => s.Contains("temp"))))
                .Returns(tempBlob.Object);
            _containerClient
                .Setup(c => c.GetBlobClient("1/old.png"))
                .Returns(deleteBlob.Object);
            _containerClient
                .Setup(c => c.GetBlobClient("1/new.png"))
                .Returns(imageBlob.Object);

            var exception = await Record.ExceptionAsync(() => CreateStorage().UpdateImageAsync(item));

            Assert.Null(exception);
            deleteBlob.Verify(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
            imageBlob.Verify(b => b.StartCopyFromUriAsync(
                It.IsAny<Uri>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<AccessTier?>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<RehydratePriority?>(),
                It.IsAny<CancellationToken>()), Times.Once);
            tempBlob.Verify(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateImageAsync_WhenBlobOperationFails_LogsAndThrows()
        {
            var item = new CatalogItem { Id = 1, TempImageName = "/pics/1/temp/new.png" };

            _containerClient
                .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("boom"));

            _containerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(SetupBlobClient().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStorage().UpdateImageAsync(item));

            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // --- UploadTempImageAsync ---

        [Fact]
        public async Task UploadTempImageAsync_WithCatalogItemId_UploadsAndReturnsUri()
        {
            SetupCreateIfNotExists();
            var blobUri = AccountUri + "pics/5/temp/photo.png";
            var blobClient = SetupBlobClient(blobUri);
            blobClient
                .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, default, default, default, default, default), Mock.Of<Response>()));
            _containerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(blobClient.Object);

            var file = CreateFormFile("Photo.PNG", "image/png");

            var result = await CreateStorage().UploadTempImageAsync(file, 5);

            Assert.Equal(blobUri, result);
            _containerClient.Verify(c => c.GetBlobClient(It.Is<string>(s => s == "5/temp/photo.png")), Times.Once);
            blobClient.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UploadTempImageAsync_WithoutCatalogItemId_UsesGuidTempPath()
        {
            SetupCreateIfNotExists();
            var blobClient = SetupBlobClient();
            blobClient
                .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, default, default, default, default, default), Mock.Of<Response>()));
            _containerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(blobClient.Object);

            var file = CreateFormFile("photo.png", "image/png");

            var result = await CreateStorage().UploadTempImageAsync(file, null);

            Assert.NotNull(result);
            _containerClient.Verify(c => c.GetBlobClient(It.Is<string>(s => s.StartsWith("temp/") && s.EndsWith("/photo.png"))), Times.Once);
        }

        [Fact]
        public async Task UploadTempImageAsync_WhenUploadFails_LogsAndThrows()
        {
            SetupCreateIfNotExists();
            var blobClient = SetupBlobClient();
            blobClient
                .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
            _containerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(blobClient.Object);

            var file = CreateFormFile("photo.png", "image/png");

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStorage().UploadTempImageAsync(file, 5));

            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private static IFormFile CreateFormFile(string fileName, string contentType)
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.FileName).Returns(fileName);
            file.SetupGet(f => f.ContentType).Returns(contentType);
            file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 1, 2, 3 }));
            return file.Object;
        }
    }
}
