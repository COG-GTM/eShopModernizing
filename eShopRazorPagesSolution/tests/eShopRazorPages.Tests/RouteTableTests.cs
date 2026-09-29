using eShopRazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eShopRazorPages.Tests;

/// <summary>Checks the endpoint table against the legacy RouteConfig mapping in <see cref="LegacyRoutes"/>.</summary>
public class RouteTableTests : IClassFixture<CatalogAppFactory>
{
    private readonly CatalogAppFactory _factory;

    public RouteTableTests(CatalogAppFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> LegacyTemplates()
    {
        var data = new TheoryData<string>();
        foreach (var route in LegacyRoutes.All)
        {
            data.Add(route.Template);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LegacyTemplates))]
    public void Every_legacy_route_is_registered_for_its_razor_page(string template)
    {
        var route = LegacyRoutes.All.Single(r => r.Template == template);

        var endpoint = Endpoints().SingleOrDefault(e =>
            string.Equals(e.RoutePattern.RawText, route.Template, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(endpoint);
        Assert.Equal(route.RazorPage, endpoint!.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath);
        Assert.Equal(route.Name, endpoint.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName);
    }

    [Theory]
    [InlineData(LegacyRoutes.CreateProductRouteName, null, "/Catalog/Create")]
    [InlineData(LegacyRoutes.EditProductRouteName, 7, "/Catalog/Edit/7")]
    [InlineData(LegacyRoutes.ProductDetailsRouteName, 7, "/Catalog/Details/7")]
    [InlineData(LegacyRoutes.DeleteProductRouteName, 7, "/Catalog/Delete/7")]
    public void Named_legacy_routes_generate_legacy_urls(string routeName, int? id, string expected)
    {
        var linkGenerator = _factory.Services.GetRequiredService<LinkGenerator>();

        var url = linkGenerator.GetPathByRouteValues(routeName, id is null ? null : new { id });

        Assert.Equal(expected, url);
    }

    [Fact]
    public void Products_by_page_route_generates_legacy_url()
    {
        var linkGenerator = _factory.Services.GetRequiredService<LinkGenerator>();

        var url = linkGenerator.GetPathByRouteValues(LegacyRoutes.ProductsByPageRouteName, new { index = 3, size = 25 });

        Assert.Equal("/Default/index/3/size/25", url);
    }

    [Fact]
    public void List_page_link_generation_prefers_root_url()
    {
        var linkGenerator = _factory.Services.GetRequiredService<LinkGenerator>();

        Assert.Equal("/", linkGenerator.GetPathByPage("/Index"));
    }

    private IEnumerable<RouteEndpoint> Endpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
}
