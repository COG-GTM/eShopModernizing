using System.Net;
using System.Net.Http.Json;
using Catalog.Api.Models;
using Catalog.Api.ViewModel;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Catalog.Api.Tests;

// End-to-end test of the ported REST surface using the real ASP.NET Core
// pipeline (TestServer). Equivalent coverage to manually browsing the legacy
// MVC /Catalog pages, now exercised as automated tests.
public class CatalogApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogApiIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // Deserialization target matching the serialized shape of
    // PaginatedItemsViewModel (which exposes read-only properties).
    private record PagedResult(int TotalItems, int TotalPages, List<CatalogItem> Data);

    [Fact]
    public async Task Get_Catalog_ReturnsPaginatedItems()
    {
        var client = _factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult>("/api/catalog?pageSize=4&pageIndex=0");

        Assert.NotNull(page);
        Assert.Equal(12, page!.TotalItems);
        Assert.Equal(4, page.Data.Count);
    }

    [Fact]
    public async Task Get_CatalogItem_ById_ReturnsItem()
    {
        var client = _factory.CreateClient();

        var item = await client.GetFromJsonAsync<CatalogItem>("/api/catalog/2");

        Assert.NotNull(item);
        Assert.Equal(2, item!.Id);
    }

    [Fact]
    public async Task Get_UnknownItem_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/catalog/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Catalog_CreatesItem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/catalog", new CatalogItem
        {
            Name = "Timberland Boot Tee",
            CatalogBrandId = 2,
            CatalogTypeId = 2,
            Price = 25.00M
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CatalogItem>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
    }

    [Fact]
    public async Task Get_Brands_And_Types()
    {
        var client = _factory.CreateClient();

        var brands = await client.GetFromJsonAsync<List<CatalogBrand>>("/api/catalog/brands");
        var types = await client.GetFromJsonAsync<List<CatalogType>>("/api/catalog/types");

        Assert.Equal(5, brands!.Count);
        Assert.Equal(4, types!.Count);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
