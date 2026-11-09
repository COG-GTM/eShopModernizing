using System.Text;
using System.Text.Json.Nodes;
using EShop.Storefront.Tests.Infrastructure;
using Xunit;

namespace EShop.Storefront.Tests.Mvc;

/// <summary>Mirrors test_mvc.py pictures + Web API tests (/items/{id}/pic, /api/brands, /api/files).</summary>
public class MvcApiTests(MvcAppFixture app) : IClassFixture<MvcAppFixture>
{
    private StorefrontClient Mvc => app.Client;

    [SkippableFact]
    public async Task Pic_endpoint()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi, Slice.Hosting);
        var r = await Mvc.GetAsync("/items/1/pic");
        Assert.Equal(200, r.Status);
        Assert.Equal("image/png", r.ContentType);
        Assert.Equal(151640, r.Body.Length);
        Assert.Equal(404, (await Mvc.GetAsync("/items/999/pic")).Status);
    }

    [SkippableFact]
    public async Task Api_brands_json()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        var r = await Mvc.GetAsync("/api/brands", "application/json");
        Assert.Equal(200, r.Status);
        Assert.StartsWith("application/json", r.ContentType);
        var expected = JsonNode.Parse("""
            [{"Id":1,"Brand":"Azure"},{"Id":2,"Brand":".NET"},{"Id":3,"Brand":"Visual Studio"},{"Id":4,"Brand":"SQL Server"},{"Id":5,"Brand":"Other"}]
            """);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(r.Text)), r.Text);
    }

    [SkippableFact]
    public async Task Api_brands_xml_content_negotiation()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        var r = await Mvc.GetAsync("/api/brands", "application/xml");
        Assert.Equal(200, r.Status);
        Assert.StartsWith("application/xml", r.ContentType);
        Assert.Contains("<Brand>Azure</Brand>", r.Text);
    }

    [SkippableFact]
    public async Task Api_brand_by_id()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        var r = await Mvc.GetAsync("/api/brands/1", "application/json");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"Id":1,"Brand":"Azure"}"""), JsonNode.Parse(r.Text)), r.Text);
        Assert.Equal(404, (await Mvc.GetAsync("/api/brands/999")).Status);
    }

    [SkippableFact]
    public async Task Api_brand_delete_is_non_mutating()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        Assert.Equal(200, (await Mvc.SendAsync(HttpMethod.Delete, "/api/brands/1")).Status);
        Assert.Equal(404, (await Mvc.SendAsync(HttpMethod.Delete, "/api/brands/999")).Status);
        var r = await Mvc.GetAsync("/api/brands", "application/json");
        Assert.Equal(5, JsonNode.Parse(r.Text)!.AsArray().Count);
    }

    [SkippableFact]
    public async Task Api_brands_unsupported_verbs()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        static StringContent Json(string s) => new(s, Encoding.UTF8, "application/json");
        Assert.Equal(405, (await Mvc.SendAsync(HttpMethod.Post, "/api/brands", Json("""{"Id":9,"Brand":"X"}"""))).Status);
        Assert.Equal(405, (await Mvc.SendAsync(HttpMethod.Put, "/api/brands/1", Json("""{"Id":1,"Brand":"X"}"""))).Status);
    }

    [SkippableFact]
    public async Task Api_files_returns_binary_brand_list()
    {
        app.Require(Slice.CatalogData, Slice.LegacyApi);
        var r = await Mvc.GetAsync("/api/files");
        Assert.Equal(200, r.Status);
        Assert.Equal(0x00, r.Body[0]); // MS-NRBF SerializationHeaderRecord
        Assert.True(r.Body.AsSpan().IndexOf("Visual Studio"u8) >= 0);
    }
}
