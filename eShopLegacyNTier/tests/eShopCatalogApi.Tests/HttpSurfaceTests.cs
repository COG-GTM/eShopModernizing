using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace eShopCatalogApi.Tests;

public sealed class HttpSurfaceTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _http;

    public HttpSurfaceTests(CatalogApiFactory factory)
    {
        _http = factory.CreateClient();
    }

    [Fact]
    public async Task Json_UsesWcfDataMemberNames()
    {
        using var doc = JsonDocument.Parse(await _http.GetStringAsync("api/catalog/items/1"));

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("Picturefilename", out _));
        Assert.True(root.TryGetProperty("CatalogBrandId", out _));
        Assert.Equal(".NET", root.GetProperty("CatalogBrand").GetProperty("Brand").GetString());
    }

    [Fact]
    public async Task GetAvailableStock_ReturnsBareInteger()
    {
        var value = await _http.GetFromJsonAsync<int>("api/catalog/items/1/stock?date=2017-09-21");

        Assert.Equal(120, value);
    }

    [Fact]
    public async Task UpdateCatalogItem_RejectsMismatchedIds()
    {
        var response = await _http.PutAsJsonAsync("api/catalog/items/1", new { Id = 2, Name = "x", CatalogBrandId = 1, CatalogTypeId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ExposesEveryWcfOperation()
    {
        var document = await _http.GetStringAsync("swagger/v1/swagger.json");

        foreach (var operation in new[]
        {
            "FindCatalogItem", "GetCatalogBrands", "GetCatalogItems", "GetCatalogTypes", "GetAvailableStock",
            "CreateAvailableStock", "CreateCatalogItem", "UpdateCatalogItem", "RemoveCatalogItem", "GetDiscount",
        })
        {
            Assert.Contains($"\"operationId\": \"{operation}\"", document);
        }
    }

    [Fact]
    public async Task Health_ReportsHealthy()
    {
        var response = await _http.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
