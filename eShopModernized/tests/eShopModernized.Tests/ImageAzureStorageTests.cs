using Azure.Storage.Blobs;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace eShopModernized.Tests;

public class ImageAzureStorageTests
{
    private static readonly Uri AccountUri = new("https://myaccount.blob.core.windows.net/");

    private static (ImageAzureStorage Service, BlobServiceClient Client) CreateService(Uri? uri = null)
    {
        var client = new BlobServiceClient(uri ?? AccountUri);
        var configuration = new Mock<IConfiguration>();
        var catalogConfiguration = new Mock<ICatalogConfiguration>();
        var logger = new Mock<ILogger<ImageAzureStorage>>();

        var service = new ImageAzureStorage(
            client,
            configuration.Object,
            catalogConfiguration.Object,
            logger.Object);

        return (service, client);
    }

    [Fact]
    public void BaseUrl_ReturnsBlobServiceUri()
    {
        var (service, client) = CreateService();

        Assert.Equal(client.Uri.ToString(), service.BaseUrl());
    }

    [Fact]
    public void UrlDefaultImage_PointsToTempDefaultBlob()
    {
        var (service, client) = CreateService();

        Assert.Equal($"{client.Uri}pics/temp/default.png", service.UrlDefaultImage());
    }

    [Fact]
    public void BuildUrlImage_WithPictureFileName_ReturnsItemScopedBlobUrl()
    {
        var (service, client) = CreateService();
        var item = new CatalogItem { Id = 9, PictureFileName = "shirt.png" };

        Assert.Equal($"{client.Uri}pics/9/shirt.png", service.BuildUrlImage(item));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void BuildUrlImage_WithoutPictureFileName_ReturnsDefaultImage(string? pictureFileName)
    {
        var (service, _) = CreateService();
        var item = new CatalogItem { Id = 9, PictureFileName = pictureFileName! };

        Assert.Equal(service.UrlDefaultImage(), service.BuildUrlImage(item));
    }

    [Fact]
    public void Uris_HonorConfiguredBlobEndpoint()
    {
        var (service, client) = CreateService(new Uri("https://another.blob.core.windows.net/"));

        Assert.StartsWith(client.Uri.ToString(), service.BaseUrl());
        Assert.StartsWith(client.Uri.ToString(), service.UrlDefaultImage());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task UpdateImageAsync_WithoutTempImageName_ReturnsWithoutContactingStorage(string? tempImageName)
    {
        var (service, _) = CreateService();
        var item = new CatalogItem { Id = 5, TempImageName = tempImageName };

        var exception = await Record.ExceptionAsync(() => service.UpdateImageAsync(item));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var (service, _) = CreateService();

        var exception = Record.Exception(() => service.Dispose());

        Assert.Null(exception);
    }
}
