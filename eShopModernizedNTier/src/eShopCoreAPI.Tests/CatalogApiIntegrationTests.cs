using System.Net;
using System.Net.Http.Json;
using eShopCoreAPI.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace eShopCoreAPI.Tests;

public class CatalogApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CatalogApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureFlags:UseMockData", "true");
        }).CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/catalog/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/catalog/items?pageSize=0")]
    [InlineData("/api/catalog/items?pageSize=101")]
    [InlineData("/api/catalog/items?pageIndex=-1")]
    public async Task GetItems_InvalidPagination_ReturnsBadRequest(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/catalog/items/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBrands_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/catalog/brands");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTypes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/catalog/types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateItem_MissingName_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/catalog/items", new { price = 10.0, catalogBrandId = 1, catalogTypeId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateItem_Valid_ReturnsCreated()
    {
        var item = new CatalogItem { Name = "Integration Mug", Price = 10M, CatalogBrandId = 1, CatalogTypeId = 1 };

        var response = await _client.PostAsJsonAsync("/api/catalog/items", item);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CatalogItem>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
    }

    [Fact]
    public async Task UpdateItem_IdMismatch_ReturnsBadRequest()
    {
        var item = new CatalogItem { Id = 2, Name = "Mismatch", Price = 1M, CatalogBrandId = 1, CatalogTypeId = 1 };

        var response = await _client.PutAsJsonAsync("/api/catalog/items/1", item);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
