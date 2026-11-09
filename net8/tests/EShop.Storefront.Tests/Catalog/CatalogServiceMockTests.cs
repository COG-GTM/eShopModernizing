using EShop.Catalog.Contracts;
using EShop.Storefront.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EShop.Storefront.Tests.Catalog;

/// <summary>
/// ICatalogService in mock mode (legacy CatalogServiceMock + PreconfiguredData), resolved from the real app DI.
/// Each test builds its own host, so every test sees the freshly seeded singleton.
/// </summary>
public class CatalogServiceMockTests
{
    private static readonly string[] SeedNames =
    [
        ".NET Bot Black Hoodie", ".NET Black & White Mug", "Prism White T-Shirt", ".NET Foundation T-shirt", "Roslyn Red Sheet",
        ".NET Blue Hoodie", "Roslyn Red T-Shirt", "Kudu Purple Hoodie", "Cup<T> White Mug", ".NET Foundation Sheet", "Cup<T> Sheet",
        "Prism White TShirt",
    ];

    private sealed class Host : IAsyncDisposable
    {
        private readonly StorefrontFactory<EShop.Storefront.Mvc.Program> _factory = new();
        private readonly IServiceScope _scope;

        public Host()
        {
            var services = _factory.Services;
            Skip.If(!StorefrontFixture<EShop.Storefront.Mvc.Program>.RequireAll
                    && !StorefrontFixture<EShop.Storefront.Mvc.Program>.HasCatalogService(services),
                "scaffold stub, not merged yet: T1 catalog data");
            _scope = services.CreateScope();
            Service = _scope.ServiceProvider.GetRequiredService<ICatalogService>();
        }

        public ICatalogService Service { get; }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _factory.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Seeds_twelve_items_five_brands_four_types()
    {
        await using var host = new Host();
        var page = host.Service.GetCatalogItemsPaginated(100, 0);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(SeedNames, page.Data.Select(i => i.Name));
        Assert.Equal(Enumerable.Range(1, 12), page.Data.Select(i => i.Id));
        Assert.Equal(["Azure", ".NET", "Visual Studio", "SQL Server", "Other"], host.Service.GetCatalogBrands().Select(b => b.Brand));
        Assert.Equal([1, 2, 3, 4, 5], host.Service.GetCatalogBrands().Select(b => b.Id));
        Assert.Equal(["Mug", "T-Shirt", "Sheet", "USB Memory Stick"], host.Service.GetCatalogTypes().Select(t => t.Type));
        Assert.Equal([1, 2, 3, 4], host.Service.GetCatalogTypes().Select(t => t.Id));
    }

    [SkippableTheory]
    [InlineData(10, 0, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2)]
    [InlineData(10, 1, new[] { 11, 12 }, 2)]
    [InlineData(10, 2, new int[0], 2)]
    [InlineData(5, 2, new[] { 11, 12 }, 3)]
    [InlineData(20, 0, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 1)]
    [InlineData(1, 11, new[] { 12 }, 12)]
    [InlineData(3, 3, new[] { 10, 11, 12 }, 4)]
    public async Task Paginates_ordered_by_id(int pageSize, int pageIndex, int[] ids, int totalPages)
    {
        await using var host = new Host();
        var page = host.Service.GetCatalogItemsPaginated(pageSize, pageIndex);
        Assert.Equal(ids, page.Data.Select(i => i.Id));
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(totalPages, page.TotalPages);
        Assert.Equal(pageIndex, page.ActualPage);
        Assert.Equal(pageSize, page.ItemsPerPage);
    }

    [SkippableFact]
    public async Task Page_size_zero_throws_like_legacy()
    {
        await using var host = new Host();
        Assert.Throws<DivideByZeroException>(() => host.Service.GetCatalogItemsPaginated(0, 0));
    }

    [SkippableFact]
    public async Task Paged_items_have_brand_and_type_resolved()
    {
        await using var host = new Host();
        var first = host.Service.GetCatalogItemsPaginated(10, 0).Data.First();
        Assert.Equal(".NET", first.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", first.CatalogType?.Type);
        Assert.Equal(19.5m, first.Price);
        Assert.Equal("1.png", first.PictureFileName);
        Assert.Equal(100, first.AvailableStock);
    }

    [SkippableFact]
    public async Task Find_returns_item_or_null()
    {
        await using var host = new Host();
        Assert.Equal("Roslyn Red Sheet", host.Service.FindCatalogItem(5)?.Name);
        Assert.Equal(8.5m, host.Service.FindCatalogItem(5)?.Price);
        Assert.Null(host.Service.FindCatalogItem(999));
    }

    [SkippableFact]
    public async Task Create_assigns_max_id_plus_one_and_resolves_navigation()
    {
        await using var host = new Host();
        var item = new CatalogItem
        {
            Name = "Transcript Item", Description = "Created by the transcript recorder", Price = 12.34m,
            CatalogBrandId = 2, CatalogTypeId = 1, AvailableStock = 10, RestockThreshold = 2, MaxStockThreshold = 50,
        };
        host.Service.CreateCatalogItem(item);
        Assert.Equal(13, item.Id);

        var page = host.Service.GetCatalogItemsPaginated(100, 0);
        Assert.Equal(13, page.TotalItems);
        var created = page.Data.Last();
        Assert.Equal((13, "Transcript Item", ".NET", "Mug", 12.34m, CatalogItem.DefaultPictureName),
            (created.Id, created.Name, created.CatalogBrand?.Brand, created.CatalogType?.Type, created.Price, created.PictureFileName));
    }

    [SkippableFact]
    public async Task Update_replaces_item()
    {
        await using var host = new Host();
        var original = host.Service.FindCatalogItem(1)!;
        host.Service.UpdateCatalogItem(new CatalogItem
        {
            Id = 1, Name = "Edited Hoodie", Description = original.Description, Price = 21.75m, PictureFileName = original.PictureFileName,
            CatalogBrandId = 3, CatalogTypeId = 2, AvailableStock = 7,
        });

        var edited = host.Service.GetCatalogItemsPaginated(10, 0).Data.First();
        Assert.Equal((1, "Edited Hoodie", 21.75m, 7, "Visual Studio", "T-Shirt"),
            (edited.Id, edited.Name, edited.Price, edited.AvailableStock, edited.CatalogBrand?.Brand, edited.CatalogType?.Type));
        Assert.Equal(12, host.Service.GetCatalogItemsPaginated(100, 0).TotalItems);
    }

    [SkippableFact]
    public async Task Remove_deletes_item()
    {
        await using var host = new Host();
        host.Service.RemoveCatalogItem(host.Service.FindCatalogItem(2)!);
        Assert.Null(host.Service.FindCatalogItem(2));
        var page = host.Service.GetCatalogItemsPaginated(100, 0);
        Assert.Equal(11, page.TotalItems);
        Assert.DoesNotContain(2, page.Data.Select(i => i.Id));
    }
}
