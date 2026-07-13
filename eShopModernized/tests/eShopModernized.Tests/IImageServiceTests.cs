using Azure.Storage.Blobs;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace eShopModernized.Tests;

public class IImageServiceTests
{
    private static ImageMockStorage CreateMockStorage()
    {
        return new ImageMockStorage(
            new Mock<IWebHostEnvironment>().Object,
            new Mock<ILogger<ImageMockStorage>>().Object);
    }

    private static ImageAzureStorage CreateAzureStorage()
    {
        return new ImageAzureStorage(
            new BlobServiceClient(new Uri("https://myaccount.blob.core.windows.net/")),
            new Mock<IConfiguration>().Object,
            new Mock<ICatalogConfiguration>().Object,
            new Mock<ILogger<ImageAzureStorage>>().Object);
    }

    public static IEnumerable<object[]> Implementations()
    {
        yield return new object[] { CreateMockStorage() };
        yield return new object[] { CreateAzureStorage() };
    }

    [Fact]
    public void Implementations_AreDisposable()
    {
        Assert.IsAssignableFrom<IDisposable>(CreateMockStorage());
        Assert.IsAssignableFrom<IDisposable>(CreateAzureStorage());
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void BuildUrlImage_WithoutPictureFileName_FallsBackToDefaultImage(IImageService service)
    {
        var item = new CatalogItem { Id = 1, PictureFileName = string.Empty };

        Assert.Equal(service.UrlDefaultImage(), service.BuildUrlImage(item));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void BaseUrl_IsNonEmpty(IImageService service)
    {
        Assert.False(string.IsNullOrEmpty(service.BaseUrl()));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void BuildUrlImage_WithPictureFileName_IncludesItemIdAndFileName(IImageService service)
    {
        var item = new CatalogItem { Id = 123, PictureFileName = "gadget.png" };

        var url = service.BuildUrlImage(item);

        Assert.Contains("123", url);
        Assert.Contains("gadget.png", url);
        Assert.NotEqual(service.UrlDefaultImage(), url);
    }
}
