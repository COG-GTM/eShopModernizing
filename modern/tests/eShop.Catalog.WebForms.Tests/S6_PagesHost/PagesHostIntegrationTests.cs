using System.Net;
using eShop.Catalog.WebForms.Hosting;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.WebForms.Tests.S6_PagesHost;

public class PagesHostIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public PagesHostIntegrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    /// <summary>Every URL legacy RouteConfig (and its .aspx targets) serves, with the legacy Mono/xsp4 status.</summary>
    public static TheoryData<string, string, int> LegacyUrls => new()
    {
        { "/Default", "/Index", 200 },
        { "/Default.aspx", "/Index", 200 },
        { "/Default/index/1/size/10", "/Index", 200 },
        { "/Catalog/Create", "/Catalog/Create", 200 },
        { "/Catalog/Create.aspx", "/Catalog/Create", 200 },
        { "/Catalog/Edit/1", "/Catalog/Edit", 200 },
        { "/Catalog/Edit.aspx", "/Catalog/Edit", 500 },
        { "/Catalog/Details/1", "/Catalog/Details", 200 },
        { "/Catalog/Details.aspx", "/Catalog/Details", 500 },
        { "/Catalog/Delete/1", "/Catalog/Delete", 200 },
        { "/Catalog/Delete.aspx", "/Catalog/Delete", 500 },
        { "/About", "/About", 200 },
        { "/About.aspx", "/About", 200 },
        { "/Contact", "/Contact", 200 },
        { "/Contact.aspx", "/Contact", 200 },
    };

    [Theory]
    [MemberData(nameof(LegacyUrls))]
    public async Task Legacy_url_gives_legacy_status_and_no_redirect(string url, string page, int legacyStatus)
    {
        // Pages owned by S7/S8 are discovered once those slices land; until then their URLs must be a clean 404.
        var pagePresent = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<PageActionDescriptor>()
            .Any(d => string.Equals(d.ViewEnginePath, page, StringComparison.Ordinal));
        using var client = factory.NoRedirectClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(pagePresent ? legacyStatus : 404, (int)response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("/About", "03-About-aspx.txt")]
    [InlineData("/About.aspx", "03-About-aspx.txt")]
    [InlineData("/Contact", "04-Contact-aspx.txt")]
    [InlineData("/Contact.aspx", "04-Contact-aspx.txt")]
    public async Task About_and_Contact_match_the_legacy_render(string url, string baseline)
    {
        using var client = factory.NoRedirectClient();
        var html = await client.GetOkHtml(url);

        var expected = S6Host.MaskPageState(S6Host.LegacyBaselineBody(baseline))
            .Replace("action=\"About.aspx\"", "action=\"" + url.TrimStart('/') + "\"")
            .Replace("action=\"Contact.aspx\"", "action=\"" + url.TrimStart('/') + "\"");
        Assert.Equal(expected, S6Host.MaskPageState(S6Host.NormaliseHtml(html)));
    }

    [Theory]
    [InlineData("/About", "About")]
    [InlineData("/Contact.aspx", "Contact")]
    public async Task Layout_renders_Site_Master_chrome(string url, string title)
    {
        using var client = factory.NoRedirectClient();
        var html = await client.GetOkHtml(url);

        Assert.Contains("<title>\n\t" + title + " - Catalog manager (Web Forms)\n</title>", html.Replace("\r", string.Empty));
        Assert.Contains("<header class=\"navbar navbar-light navbar-static-top\">", html);
        Assert.Contains("<div class=\"esh-header-brand\">", html);
        Assert.Contains("<img src=\"/images/brand.png\" />", html);
        Assert.Contains("<section class=\"esh-app-hero\">", html);
        Assert.Contains("<div class=\"container esh-header\">", html);
        Assert.Contains("<h1 class=\"esh-header-title\">Catalog manager <span>(Web Forms)</span></h1>", html);
        Assert.Contains("<footer class=\"esh-app-footer\">", html);
        Assert.Contains("<img class=\"esh-app-footer-brand\" src=\"/images/brand_dark.png\" />", html);
        Assert.Contains("<img class=\"esh-app-footer-text hidden-xs\" src=\"/images/main_footer_text.png\" width=\"335\" height=\"26\" alt=\"footer text image\" />", html);
        Assert.StartsWith(Environment.MachineName + ", ", S6Host.Footer(html));
        Assert.DoesNotContain("ScriptManager", html);
        Assert.DoesNotContain("Sys.WebForms", html);
    }

    [Fact]
    public async Task Layout_emits_the_scripts_ScriptManager_and_bundles_rendered_in_order()
    {
        using var client = factory.NoRedirectClient();
        var html = await client.GetOkHtml("/About");

        string[] scripts =
        [
            "/Scripts/modernizr-2.8.3.js",
            "/Scripts/WebForms/MsAjax/MicrosoftAjax.js",
            "/Scripts/WebForms/MsAjax/MicrosoftAjaxApplicationServices.js",
            "/Scripts/WebForms/MsAjax/MicrosoftAjaxTimer.js",
            "/Scripts/WebForms/MsAjax/MicrosoftAjaxWebForms.js",
            "/Scripts/jquery-3.3.1.js",
            "/Scripts/bootstrap.js",
            "/Scripts/respond.js",
            "/Scripts/WebForms/WebForms.js",
            "/Scripts/WebForms/WebUIValidation.js",
            "/Scripts/WebForms/MenuStandards.js",
            "/Scripts/WebForms/GridView.js",
            "/Scripts/WebForms/DetailsView.js",
            "/Scripts/WebForms/TreeView.js",
            "/Scripts/WebForms/WebParts.js",
            "/Scripts/WebForms/Focus.js",
        ];
        var positions = scripts.Select(s => html.IndexOf("<script src=\"" + s + "\"", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);

        foreach (var script in scripts)
        {
            using var asset = await client.GetAsync(script);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        }
    }

    [Fact]
    public async Task Footer_timestamp_is_stable_within_a_session_and_changes_for_a_new_one()
    {
        var clock = new ManualTimeProvider();
        using var app = factory.WithClock(clock);
        using var first = app.NoRedirectClient();

        var start = S6Host.Footer(await first.GetOkHtml("/About"));
        clock.Advance(TimeSpan.FromMinutes(3));
        var later = S6Host.Footer(await first.GetOkHtml("/Contact.aspx"));
        using var second = app.NoRedirectClient();
        var other = S6Host.Footer(await second.GetOkHtml("/About"));

        Assert.Equal($"{Environment.MachineName}, 3/5/2024 2:07:09 PM", start);
        Assert.Equal(start, later);
        Assert.Equal($"{Environment.MachineName}, 3/5/2024 2:10:09 PM", other);
    }

    [Fact]
    public async Task Pages_opt_in_to_the_legacy_page_form_only()
    {
        using var client = factory.NoRedirectClient();
        var html = await client.GetOkHtml("/About.aspx");

        Assert.Contains("<form method=\"post\" action=\"About.aspx\" id=\"ctl00_ctl01\">", html);
        Assert.Contains("<div class=\"aspNetHidden\">", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Equal(1, html.Split("<form").Length - 1);
    }

    /// <summary>Legacy static asset URLs (as written in Site.Master, bundles, CSS and the catalog pages) in both casings.</summary>
    public static TheoryData<string, string, string> StaticAssets => new()
    {
        { "/Content/site.css", "Content/site.css", "text/css" },
        { "/Content/Site.css", "Content/site.css", "text/css" },
        { "/Content/bootstrap.css", "Content/bootstrap.css", "text/css" },
        { "/Content/custom.css", "Content/custom.css", "text/css" },
        { "/Content/base.css", "Content/base.css", "text/css" },
        { "/content/BASE.css", "Content/base.css", "text/css" },
        { "/Scripts/modernizr-2.8.3.js", "Scripts/modernizr-2.8.3.js", "application/javascript" },
        { "/Scripts/jquery-3.3.1.js", "Scripts/jquery-3.3.1.js", "application/javascript" },
        { "/Scripts/bootstrap.js", "Scripts/bootstrap.js", "application/javascript" },
        { "/Scripts/respond.js", "Scripts/respond.js", "application/javascript" },
        { "/Scripts/WebForms/MsAjax/MicrosoftAjax.js", "Scripts/WebForms/MSAjax/MicrosoftAjax.js", "application/javascript" },
        { "/Scripts/WebForms/MSAjax/MicrosoftAjax.js", "Scripts/WebForms/MSAjax/MicrosoftAjax.js", "application/javascript" },
        { "/Scripts/WebForms/MsAjax/MicrosoftAjaxWebForms.js", "Scripts/WebForms/MSAjax/MicrosoftAjaxWebForms.js", "application/javascript" },
        { "/Scripts/WebForms/WebForms.js", "Scripts/WebForms/WebForms.js", "application/javascript" },
        { "/scripts/webforms/webuivalidation.js", "Scripts/WebForms/WebUIValidation.js", "application/javascript" },
        { "/images/brand.png", "images/brand.png", "image/png" },
        { "/Images/brand.png", "images/brand.png", "image/png" },
        { "/images/brand_dark.png", "images/brand_dark.png", "image/png" },
        { "/Images/brand_dark.png", "images/brand_dark.png", "image/png" },
        { "/images/main_footer_text.png", "images/main_footer_text.png", "image/png" },
        { "/images/main_banner.png", "images/main_banner.png", "image/png" },
        { "/Images/main_banner.png", "images/main_banner.png", "image/png" },
        { "/favicon.ico", "favicon.ico", "image/x-icon" },
        { "/Favicon.ico", "favicon.ico", "image/x-icon" },
        { "/fonts/Montserrat-Bold.woff", "fonts/Montserrat-Bold.woff", "application/font-woff" },
        { "/Fonts/Montserrat-Regular.ttf", "fonts/Montserrat-Regular.ttf", "application/x-font-ttf" },
        { "/fonts/Montserrat-Regular.svg", "fonts/Montserrat-Regular.svg", "image/svg+xml" },
        { "/fonts/Montserrat-Bold.eot", "fonts/Montserrat-Bold.eot", "application/vnd.ms-fontobject" },
        { "/fonts/Montserrat-Bold.woff2", "fonts/Montserrat-Bold.woff2", "application/octet-stream" },
        { "/Pics/1.png", "Pics/1.png", "image/png" },
        { "/pics/12.png", "Pics/12.png", "image/png" },
    };

    [Theory]
    [MemberData(nameof(StaticAssets))]
    public async Task Static_asset_is_served_for_legacy_casing_with_content_type(string url, string file, string contentType)
    {
        using var client = factory.NoRedirectClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(S6Host.WwwRoot, file)), await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("/nope.css")]
    [InlineData("/Content/../appsettings.json")]
    [InlineData("/Content/%2e%2e/appsettings.json")]
    public async Task Missing_or_traversing_static_paths_are_404_with_an_html_body(string url)
    {
        using var client = factory.NoRedirectClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("The resource cannot be found.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Requests_run_with_the_en_US_culture_regardless_of_Accept_Language()
    {
        var clock = new ManualTimeProvider();
        using var app = factory.WithClock(clock);
        using var client = app.NoRedirectClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE");

        var footer = S6Host.Footer(await client.GetOkHtml("/About"));

        Assert.EndsWith(", 3/5/2024 2:07:09 PM", footer);
    }
}
