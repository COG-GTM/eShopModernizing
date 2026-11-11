using System.Net;
using System.Text;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Mvc.Tests.S5_MvcWebApi;

/// <summary>Bodies and headers compared with responses recorded from the legacy app (parity/baseline + manual probes).</summary>
public class WebApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string LegacyBrandsJson =
        "[{\"Id\":1,\"Brand\":\"Azure\"},{\"Id\":2,\"Brand\":\".NET\"},{\"Id\":3,\"Brand\":\"Visual Studio\"}," +
        "{\"Id\":4,\"Brand\":\"SQL Server\"},{\"Id\":5,\"Brand\":\"Other\"}]";

    private const string LegacyXmlNs =
        "xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models\"";

    private readonly WebApplicationFactory<Program> factory;

    public WebApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_brands_returns_exact_legacy_json()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/brands", "application/json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(LegacyBrandsJson, await response.Content.ReadAsStringAsync());
        AssertWebApiCacheHeaders(response);
    }

    [Theory]
    [InlineData("/api/brands/2")]
    [InlineData("/API/BRANDS/2/")]
    [InlineData("/api/brands?id=2")]
    [InlineData("/api/brands/0x2")]
    [InlineData("/api/brands/5?id=2")]
    public async Task Get_brand_by_id_returns_exact_legacy_json(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, accept: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"Id\":2,\"Brand\":\".NET\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_brands_with_accept_xml_returns_legacy_data_contract_xml()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/brands", "application/xml");

        Assert.Equal("application/xml; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(
            $"<ArrayOfCatalogBrand {LegacyXmlNs}>" +
            "<CatalogBrand><Brand>Azure</Brand><Id>1</Id></CatalogBrand><CatalogBrand><Brand>.NET</Brand><Id>2</Id></CatalogBrand>" +
            "<CatalogBrand><Brand>Visual Studio</Brand><Id>3</Id></CatalogBrand><CatalogBrand><Brand>SQL Server</Brand><Id>4</Id></CatalogBrand>" +
            "<CatalogBrand><Brand>Other</Brand><Id>5</Id></CatalogBrand></ArrayOfCatalogBrand>",
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", "application/xml; charset=utf-8")]
    [InlineData("text/xml", "text/xml; charset=utf-8")]
    [InlineData("text/json", "text/json; charset=utf-8")]
    [InlineData("*/*", "application/json; charset=utf-8")]
    [InlineData("text/plain", "application/json; charset=utf-8")]
    public async Task Get_brand_negotiates_content_type_like_legacy(string accept, string expectedContentType)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/brands/2", accept);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(
            expectedContentType.Contains("xml")
                ? $"<CatalogBrand {LegacyXmlNs}><Brand>.NET</Brand><Id>2</Id></CatalogBrand>"
                : "{\"Id\":2,\"Brand\":\".NET\"}",
            body);
    }

    [Theory]
    [InlineData("/api/brands/999", null)]
    [InlineData("/api/brands/999", "application/xml")]
    [InlineData("/api/brands/-1", "application/json")]
    public async Task Get_missing_brand_returns_404_with_empty_body(string path, string? accept)
    {
        using var response = await SendAsync(HttpMethod.Get, path, accept);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        AssertWebApiCacheHeaders(response);
    }

    [Fact]
    public async Task Empty_404_stays_empty_even_when_the_host_adds_status_code_pages()
    {
        using var withStatusPages = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddTransient<IStartupFilter, StatusCodePagesStartupFilter>()));
        using var client = withStatusPages.CreateClient();

        using var apiResponse = await client.GetAsync("/api/brands/999");
        using var otherResponse = await client.GetAsync("/no-such-page");

        Assert.Equal(HttpStatusCode.NotFound, apiResponse.StatusCode);
        Assert.Empty(await apiResponse.Content.ReadAsByteArrayAsync());
        Assert.NotEmpty(await otherResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Delete_existing_brand_returns_200_empty_and_changes_nothing()
    {
        using var response = await SendAsync(HttpMethod.Delete, "/api/brands/1", accept: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var brands = scope.ServiceProvider.GetRequiredService<ICatalogService>().GetCatalogBrands().ToList();
            Assert.Equal(5, brands.Count);
            Assert.Contains(brands, b => b.Id == 1 && b.Brand == "Azure");
        }

        using var after = await SendAsync(HttpMethod.Get, "/api/brands", "application/json");
        Assert.Equal(LegacyBrandsJson, await after.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/brands/999")]
    [InlineData("/api/brands?id=999")]
    public async Task Delete_missing_brand_returns_404_empty(string path)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, accept: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("GET", "/api/brands/abc", "Get")]
    [InlineData("GET", "/api/brands?id=", "Get")]
    [InlineData("GET", "/api/brands/1.5", "Get")]
    [InlineData("DELETE", "/api/brands/abc", "Delete")]
    public async Task Unbindable_id_returns_legacy_400_http_error(string method, string path, string action)
    {
        using var response = await SendAsync(new HttpMethod(method), path, accept: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(
            "{\"Message\":\"The request is invalid.\",\"MessageDetail\":\"The parameters dictionary contains a null entry for parameter 'id' " +
            $"of non-nullable type 'System.Int32' for method 'System.Web.Http.IHttpActionResult {action}(Int32)' in " +
            "'eShopLegacyMVC.Controllers.WebApi.BrandsController'. An optional parameter must be a reference type, a nullable type, " +
            "or be declared as an optional parameter.\"}",
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("POST", "/api/brands", "GET")]
    [InlineData("DELETE", "/api/brands", "GET")]
    [InlineData("PUT", "/api/brands/1", "GET,DELETE")]
    [InlineData("PATCH", "/api/brands?id=1", "GET,DELETE")]
    [InlineData("OPTIONS", "/api/brands", "GET")]
    [InlineData("DELETE", "/api/files", "GET")]
    [InlineData("POST", "/api/files/1", "GET")]
    public async Task Unsupported_method_returns_legacy_405(string method, string path, string allow)
    {
        using var response = await SendAsync(new HttpMethod(method), path, accept: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(allow.Split(','), response.Content.Headers.Allow);
        Assert.Equal($"{{\"Message\":\"The requested resource does not support http method '{method}'.\"}}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Head_is_not_mapped_to_get_like_legacy()
    {
        using var response = await SendAsync(HttpMethod.Head, "/api/brands", accept: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_method_with_accept_xml_returns_xml_http_error()
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/brands", "application/xml");

        Assert.Equal("application/xml; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("<Error><Message>The requested resource does not support http method 'POST'.</Message></Error>", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("application/xml")]
    [InlineData("application/octet-stream")]
    public async Task Get_files_returns_json_never_binary_formatter(string? accept)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/files", accept);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEqual(0x00, bytes[0]); // BinaryFormatter streams start with record type 0x00 (SerializationHeader)
        Assert.Equal(LegacyBrandsJson, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Get_files_ignores_trailing_id_like_legacy()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/files/abc", accept: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LegacyBrandsJson, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/")]
    public async Task Get_api_root_returns_404(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, accept: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/nothing", "nothing")]
    [InlineData("/api/Catalog/5?x=1", "Catalog")]
    public async Task Unknown_api_controller_returns_legacy_404_http_error(string path, string name)
    {
        using var response = await SendAsync(HttpMethod.Get, path, accept: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            $"{{\"Message\":\"No HTTP resource was found that matches the request URI 'http://localhost{path}'.\"," +
            $"\"MessageDetail\":\"No type was found that matches the controller named '{name}'.\"}}",
            await response.Content.ReadAsStringAsync());
    }

    private static void AssertWebApiCacheHeaders(HttpResponseMessage response)
    {
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", response.Headers.Pragma.ToString());
        Assert.Equal("-1", string.Join(",", response.Content.Headers.GetValues("Expires")));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? accept)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(method, path);
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return await client.SendAsync(request);
    }

    private sealed class StatusCodePagesStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseStatusCodePages();
            next(app);
        };
    }
}
