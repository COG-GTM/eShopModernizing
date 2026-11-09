using EShop.Storefront.Tests.Infrastructure;
using Xunit;

namespace EShop.Storefront.Tests.Mvc;

/// <summary>Mirrors test_mvc.py::test_static_content (bundles, static files, favicon).</summary>
public class MvcStaticTests(MvcAppFixture app) : IClassFixture<MvcAppFixture>
{
    [SkippableTheory]
    [InlineData("/Content/css", "text/css")]
    [InlineData("/bundles/jquery", "javascript")]
    [InlineData("/bundles/jqueryval", "javascript")]
    [InlineData("/bundles/modernizr", "javascript")]
    [InlineData("/bundles/bootstrap", "javascript")]
    [InlineData("/images/brand.png", "image/png")]
    [InlineData("/Pics/1.png", "image/png")]
    [InlineData("/favicon.ico", "image")]
    public async Task Static_content(string path, string contentType)
    {
        app.Require(Slice.Hosting);
        var r = await app.Client.GetAsync(path);
        Assert.Equal(200, r.Status);
        Assert.Contains(contentType, r.ContentType);
    }
}
