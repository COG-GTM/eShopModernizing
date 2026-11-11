using System.Net;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

/// <summary>Compares the rendered layout with the legacy MVC capture in modern/parity/baseline (GET / on eShopLegacyMVC).</summary>
public class LayoutMarkupTests : IClassFixture<S2HostFactory>
{
    private readonly S2HostFactory factory;

    public LayoutMarkupTests(S2HostFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("<head>", "</head>")]
    [InlineData("<header", "</section>")]
    [InlineData("<footer", "</html>")]
    public async Task Layout_section_matches_legacy_mvc_markup(string from, string to)
    {
        var legacy = LegacyBaseline.ReadNormalisedBody("01-root.txt");
        var modern = LegacyBaseline.NormaliseHtml(await GetLayoutPageAsync());

        Assert.Equal(LegacyBaseline.Slice(legacy, from, to), LegacyBaseline.Slice(modern, from, to));
    }

    [Fact]
    public async Task Layout_starts_with_legacy_doctype_and_body()
    {
        var modern = LegacyBaseline.NormaliseHtml(await GetLayoutPageAsync());

        Assert.StartsWith("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\" />", modern);
        Assert.Contains("\n</head>\n<body>\n<header class=\"navbar navbar-light navbar-static-top\">", modern);
    }

    [Fact]
    public async Task Layout_css_classes_match_legacy()
    {
        var html = await GetLayoutPageAsync();

        foreach (var cssClass in new[]
                 {
                     "navbar navbar-light navbar-static-top", "esh-header-brand", "esh-app-hero", "container esh-header",
                     "esh-header-title", "esh-app-footer", "esh-app-footer-brand", "esh-app-footer-text hidden-xs", "col-sm-6",
                 })
        {
            Assert.Contains($"class=\"{cssClass}\"", html);
        }
    }

    [Fact]
    public async Task Validation_scripts_partial_emits_legacy_jqueryval_bundle_urls()
    {
        var legacyCreate = LegacyBaseline.ReadNormalisedBody("07-Catalog-Create.txt");
        var partial = await File.ReadAllTextAsync(Path.Combine(
            LegacyBaseline.ModernRoot, "src", "eShop.Catalog.Mvc", "Views", "Shared", "_ValidationScriptsPartial.cshtml"));

        Assert.Contains("<script src=\"/Scripts/jquery.validate.js\">\n</script>\n<script src=\"/Scripts/jquery.validate.unobtrusive.js\">", legacyCreate);
        Assert.Contains("<script src=\"@Url.Content(\"~/Scripts/jquery.validate.js\")\"></script>", partial);
        Assert.Contains("<script src=\"@Url.Content(\"~/Scripts/jquery.validate.unobtrusive.js\")\"></script>", partial);
    }

    private async Task<string> GetLayoutPageAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/__s2-probe/layout");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsStringAsync();
    }
}
