using System.Net;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public class ErrorHandlingTests : IClassFixture<S2HostFactory>
{
    private readonly S2HostFactory factory;

    public ErrorHandlingTests(S2HostFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/no/such/route")]
    [InlineData("/a/b/c/d")]
    [InlineData("/NoSuchController")]
    [InlineData("/Content/missing.css")]
    public async Task Unknown_route_is_404_with_the_aspnet_not_found_page(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>The resource cannot be found.</title>", body);
        Assert.DoesNotContain(url, body);
    }

    [Fact]
    public async Task Action_level_404_keeps_the_legacy_empty_body()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__s2-probe/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("(empty body)", LegacyBaseline.ReadNormalisedBody("05-Catalog-Details-999.txt"));
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unhandled_exception_renders_legacy_error_view_inside_the_layout_with_500()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__s2-probe/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = LegacyBaseline.NormaliseHtml(await response.Content.ReadAsStringAsync());
        Assert.Contains("<h1>Error.\n</h1>\n<h2>An error occurred while processing your request.\n</h2>", body);
        Assert.Contains("<footer class=\"esh-app-footer\">", body);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Forwarded_proto_from_loopback_proxy_is_applied()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__s2-probe/layout");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
