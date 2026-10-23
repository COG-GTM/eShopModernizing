using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Seed;
using eShop.Catalog.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.UnitTests;

// Legacy CatalogServiceMock semantics (INVENTORY sections 4/10) through the UseMockData=true registration.
public class CatalogServiceMockTests
{
    [SkippableFact]
    public void Registered_as_singleton_when_UseMockData_is_true()
    {
        var services = TestServices.Collection(TestServices.Config());
        var d = services.LastOrDefault(s => s.ServiceType == typeof(ICatalogService));
        Skip.If(d is null, "T2 not in this build");
        Assert.Equal(ServiceLifetime.Singleton, d!.Lifetime);
        var sp = services.BuildServiceProvider();
        Assert.Same(sp.GetRequiredService<ICatalogService>(), sp.GetRequiredService<ICatalogService>());
    }

    [SkippableFact]
    public void Lookups_are_the_preconfigured_brands_and_types()
    {
        var svc = TestServices.MockService();
        Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, svc.GetCatalogBrands().Select(b => b.Brand));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, svc.GetCatalogBrands().Select(b => b.Id));
        Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" }, svc.GetCatalogTypes().Select(t => t.Type));
    }

    [SkippableTheory]
    [InlineData(10, 0, 10, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(10, 1, 10, 2, new[] { 11, 12 })]
    [InlineData(5, 2, 5, 3, new[] { 11, 12 })]
    [InlineData(10, 5, 10, 2, new int[0])]
    [InlineData(100, 0, 100, 1, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData(1, 11, 1, 12, new[] { 12 })]
    // Negative values reach LINQ unchanged (legacy mock): Skip(<0) skips nothing, Take(<0) takes nothing.
    [InlineData(10, -1, 10, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(-1, 0, -1, -12, new int[0])]
    public void Paging_matches_legacy(int size, int index, int itemsPerPage, int totalPages, int[] ids)
    {
        var page = TestServices.MockService().GetCatalogItemsPaginated(size, index);
        Assert.Equal(ids, page.Data.Select(i => i.Id));
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(index, page.ActualPage);
        Assert.Equal(itemsPerPage, page.ItemsPerPage);
        Assert.Equal(totalPages, page.TotalPages);
    }

    [SkippableFact]
    public void Page_size_zero_divides_by_zero_like_legacy_Q5()
    {
        var svc = TestServices.MockService();
        Assert.Throws<DivideByZeroException>(() => svc.GetCatalogItemsPaginated(0, 0));
    }

    [SkippableFact]
    public void Navigation_properties_are_only_composed_by_listing_Q4()
    {
        var svc = TestServices.MockService();
        var cold = svc.FindCatalogItem(12)!;
        Assert.Null(cold.CatalogBrand);
        Assert.Null(cold.CatalogType);

        svc.GetCatalogItemsPaginated(1, 0); // composes onto every stored item, not just the page
        var warm = svc.FindCatalogItem(12)!;
        Assert.Equal("Other", warm.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", warm.CatalogType?.Type);
    }

    [SkippableFact]
    public void Find_unknown_returns_null()
    {
        Assert.Null(TestServices.MockService().FindCatalogItem(999));
    }

    [SkippableFact]
    public void Each_registration_starts_from_the_seed_data()
    {
        var a = TestServices.MockService();
        a.CreateCatalogItem(new CatalogItem { Name = "x", CatalogBrandId = 1, CatalogTypeId = 1 });
        var b = TestServices.MockService();
        Assert.Equal(13, a.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(12, b.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(PreconfiguredData.GetPreconfiguredCatalogItems().Select(i => i.Name), b.GetCatalogItemsPaginated(100, 0).Data.Select(i => i.Name));
    }

    [SkippableFact]
    public void Create_assigns_max_id_plus_one()
    {
        var svc = TestServices.MockService();
        var item = new CatalogItem { Id = 500, Name = "Parity Test Hoodie", Price = 24.99m, CatalogBrandId = 3, CatalogTypeId = 2 };
        svc.CreateCatalogItem(item);
        Assert.Equal(13, item.Id);
        Assert.Same(item, svc.FindCatalogItem(13));
        var next = new CatalogItem { Name = "Second", CatalogBrandId = 1, CatalogTypeId = 1 };
        svc.CreateCatalogItem(next);
        Assert.Equal(14, next.Id);
    }

    [SkippableFact]
    public void Update_replaces_by_id_and_ignores_unknown_ids()
    {
        var svc = TestServices.MockService();
        var edited = new CatalogItem { Id = 1, Name = "Edited Hoodie", Price = 21.50m, AvailableStock = 77, CatalogBrandId = 2, CatalogTypeId = 2 };
        svc.UpdateCatalogItem(edited);
        Assert.Same(edited, svc.FindCatalogItem(1));

        svc.UpdateCatalogItem(new CatalogItem { Id = 999, Name = "Ghost" });
        Assert.Null(svc.FindCatalogItem(999));
        Assert.Equal(12, svc.GetCatalogItemsPaginated(100, 0).TotalItems);
    }

    [SkippableFact]
    public void Remove_deletes_and_is_a_no_op_for_unknown_items_Q6()
    {
        var svc = TestServices.MockService();
        svc.RemoveCatalogItem(svc.FindCatalogItem(3)!);
        Assert.Null(svc.FindCatalogItem(3));
        Assert.Equal(11, svc.GetCatalogItemsPaginated(100, 0).TotalItems);

        svc.RemoveCatalogItem(new CatalogItem { Id = 5, Name = "not the stored instance" });
        Assert.NotNull(svc.FindCatalogItem(5));
        Assert.Equal(11, svc.GetCatalogItemsPaginated(100, 0).TotalItems);
    }

    [SkippableFact]
    public async Task Concurrent_creates_get_distinct_ids()
    {
        var svc = TestServices.MockService();
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => svc.CreateCatalogItem(new CatalogItem { Name = "c" + i, CatalogBrandId = 2, CatalogTypeId = 2 }))));
        var ids = svc.GetCatalogItemsPaginated(1000, 0).Data.Select(i => i.Id).ToList();
        Assert.Equal(62, ids.Count);
        Assert.Equal(Enumerable.Range(1, 62), ids);
    }
}
