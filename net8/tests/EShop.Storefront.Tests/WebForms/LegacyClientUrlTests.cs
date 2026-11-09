using EShop.Storefront.WebForms.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EShop.Storefront.Tests.WebForms;

public class LegacyClientUrlTests
{
    [Theory]
    [InlineData("", "/", "~/", "./")]
    [InlineData("", "/Catalog/Create", "~", "../")]
    [InlineData("", "/Default/index/0/size/10", "~/favicon.ico", "../../../../favicon.ico")]
    [InlineData("/app", "/", "~/favicon.ico", "favicon.ico")]
    [InlineData("/app", "/Catalog/Details/1", "~/", "../../")]
    public void Resolves_relative_to_the_application_root(string pathBase, string path, string url, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        Assert.Equal(expected, LegacyClientUrl.Resolve(context, url));
    }
}
