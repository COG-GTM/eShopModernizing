using eShop.Catalog.WebForms.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace eShop.Catalog.WebForms.Tests.S6_PagesHost;

public class LegacyRoutingUnitTests
{
    [Fact]
    public void Alias_table_matches_RouteConfig_Default_route_and_every_MapPageRoute_target()
    {
        var expected = new[]
        {
            ("/Index", "Default"),               // routes.MapPageRoute("", "Default", "~/Default.aspx")
            ("/Index", "Default.aspx"),          // physical target of Default and ProductsByPageRoute
            ("/About", "About.aspx"),
            ("/Contact", "Contact.aspx"),
            ("/Catalog/Create", "Catalog/Create.aspx"),
            ("/Catalog/Edit", "Catalog/Edit.aspx"),
            ("/Catalog/Details", "Catalog/Details.aspx"),
            ("/Catalog/Delete", "Catalog/Delete.aspx"),
        };

        Assert.Equal(expected, LegacyPageAliases.Table);
    }

    [Fact]
    public void Alias_table_has_no_duplicate_or_rooted_templates()
    {
        Assert.Equal(LegacyPageAliases.Table.Count, LegacyPageAliases.Table.Select(a => a.Alias.ToLowerInvariant()).Distinct().Count());
        Assert.All(LegacyPageAliases.Table, a =>
        {
            Assert.StartsWith("/", a.Page);
            Assert.False(a.Alias.StartsWith('/') || a.Alias.StartsWith('~'));
        });
    }

    [Theory]
    [InlineData("/", "", "./", "Default.aspx")]
    [InlineData("/About.aspx", "", "./", "About.aspx")]
    [InlineData("/Default", "", "./", "Default")]
    [InlineData("/Catalog/Create", "../", "../", "Create")]
    [InlineData("/Catalog/Details/1", "../../", "../../", "1")]
    [InlineData("/Default/index/1/size/10", "../../../../", "../../../../", "10")]
    public void Layout_urls_resolve_like_WebForms(string path, string root, string home, string action)
    {
        Assert.Equal(root, LegacyLayout.RelativeRoot(new PathString(path)));
        Assert.Equal(home, LegacyLayout.HomeHref(new PathString(path)));
        Assert.Equal(action, LegacyLayout.FormAction(new PathString(path), QueryString.Empty));
    }

    [Fact]
    public void Form_action_keeps_the_query_string()
    {
        Assert.Equal("Default.aspx?x=1", LegacyLayout.FormAction(new PathString("/Default.aspx"), new QueryString("?x=1")));
    }

    [Theory]
    [InlineData("/Images/Brand.PNG", "/images/brand.png")]
    [InlineData("/Scripts/WebForms/MsAjax/MicrosoftAjax.js", "/Scripts/WebForms/MSAjax/MicrosoftAjax.js")]
    [InlineData("/content/SITE.css", "/Content/site.css")]
    public void Case_insensitive_provider_resolves_any_casing(string requested, string onDisk)
    {
        var provider = new CaseInsensitiveFileProvider(new PhysicalFileProvider(S6Host.WwwRoot));

        Assert.Equal(onDisk, provider.Resolve(requested));
        Assert.True(provider.GetFileInfo(requested).Exists);
    }

    [Theory]
    [InlineData("/Content/../../README.md")]
    [InlineData("/Content/./site.css")]
    [InlineData("/Content\\site.css")]
    [InlineData("/nope.css")]
    [InlineData("/")]
    public void Case_insensitive_provider_rejects_traversal_and_misses(string requested)
    {
        var provider = new CaseInsensitiveFileProvider(new PhysicalFileProvider(S6Host.WwwRoot));

        Assert.Null(provider.Resolve(requested));
    }
}
