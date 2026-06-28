using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Timberland.Catalog.Api.Models;
using Xunit;

namespace Timberland.Catalog.Api.Tests;

public class CatalogApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCatalog_ReturnsPaginatedItems()
    {
        var client = _factory.CreateClient();

        var result = await client.GetFromJsonAsync<PaginatedResult>("/api/catalog?pageSize=5&pageIndex=0");

        Assert.NotNull(result);
        Assert.Equal(12, result!.TotalItems);
        Assert.Equal(5, result.Data.Count);
    }

    // Mirrors PaginatedItemsViewModel for deserialization (the view model uses a
    // parameterized constructor that System.Text.Json cannot bind on read).
    private sealed class PaginatedResult
    {
        public int ActualPage { get; set; }
        public int ItemsPerPage { get; set; }
        public long TotalItems { get; set; }
        public int TotalPages { get; set; }
        public List<CatalogItem> Data { get; set; } = new();
    }

    [Fact]
    public async Task GetCatalogById_ReturnsItem()
    {
        var client = _factory.CreateClient();

        var item = await client.GetFromJsonAsync<CatalogItem>("/api/catalog/1");

        Assert.NotNull(item);
        Assert.Equal(".NET Bot Black Hoodie", item!.Name);
    }

    [Fact]
    public async Task Health_ReturnsTimberlandBranding()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Timberland Catalog API", body);
    }

    [Fact]
    public async Task GetCatalogById_ReturnsNotFound_WhenMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/catalog/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
