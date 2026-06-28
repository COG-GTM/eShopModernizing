using Catalog.Api.Infrastructure;
using Catalog.Api.Models;
using Catalog.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Tests;

public class CatalogServiceEfTests
{
    private static CatalogDbContext NewSeededContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new CatalogDbContext(options);
        db.EnsureSeeded();
        return db;
    }

    [Fact]
    public void GetCatalogItemsPaginated_IncludesNavigationProperties()
    {
        using var db = NewSeededContext();
        var service = new CatalogService(db);

        var page = service.GetCatalogItemsPaginated(pageSize: 10, pageIndex: 0);

        Assert.Equal(12, page.TotalItems);
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogBrand));
    }

    [Fact]
    public void CreateThenRemove_PersistsChanges()
    {
        using var db = NewSeededContext();
        var service = new CatalogService(db);

        var created = service.CreateCatalogItem(new CatalogItem { Name = "Vans Mug", CatalogBrandId = 2, CatalogTypeId = 1 });
        Assert.NotEqual(0, created.Id);
        Assert.NotNull(service.FindCatalogItem(created.Id));

        service.RemoveCatalogItem(created);
        Assert.Null(service.FindCatalogItem(created.Id));
    }
}
