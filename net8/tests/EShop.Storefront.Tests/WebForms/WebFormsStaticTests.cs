using EShop.Storefront.Tests.Infrastructure;
using Xunit;

namespace EShop.Storefront.Tests.WebForms;

/// <summary>Mirrors test_webforms.py::test_static.</summary>
public class WebFormsStaticTests(WebFormsAppFixture app) : IClassFixture<WebFormsAppFixture>
{
    [SkippableTheory]
    [InlineData("/Content/site.css", "text/css")]
    [InlineData("/images/brand.png", "image/png")]
    [InlineData("/Pics/1.png", "image/png")]
    [InlineData("/Scripts/WebForms/WebForms.js", "javascript")]
    public async Task Static(string path, string contentType)
    {
        app.Require(Slice.Hosting);
        var r = await app.Client.GetAsync(path);
        Assert.Equal(200, r.Status);
        Assert.Contains(contentType, r.ContentType);
    }
}
