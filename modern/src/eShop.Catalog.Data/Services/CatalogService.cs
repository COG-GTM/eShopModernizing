using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShop.Catalog.Data.Services;

/// <summary>
/// EF Core port of the legacy CatalogService (UseMockData=false). Scoped, like legacy InstancePerLifetimeScope.
/// Catalog ids come from the catalog_hilo HiLo generator configured on <see cref="CatalogDbContext"/>, whose block state
/// is process-wide, matching the legacy singleton CatalogItemHiLoGenerator.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext db;

    public CatalogService(CatalogDbContext db)
    {
        this.db = db;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = db.CatalogItems.LongCount();

        var itemsOnPage = db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return db.CatalogTypes.OrderBy(t => t.Id).ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return db.CatalogBrands.OrderBy(b => b.Id).ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        // Legacy always overwrote Id with the next HiLo value; Id = 0 makes EF Core's HiLo generator assign it.
        catalogItem.Id = 0;
        db.CatalogItems.Add(catalogItem);
        db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        var tracked = db.CatalogItems.Local.FirstOrDefault(ci => ci.Id == catalogItem.Id);
        if (tracked != null && !ReferenceEquals(tracked, catalogItem))
        {
            db.Entry(tracked).CurrentValues.SetValues(catalogItem);
            db.Entry(tracked).State = EntityState.Modified;
        }
        else
        {
            db.Entry(catalogItem).State = EntityState.Modified;
        }

        db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var tracked = db.CatalogItems.Local.FirstOrDefault(ci => ci.Id == catalogItem.Id);
        db.CatalogItems.Remove(tracked ?? catalogItem);
        db.SaveChanges();
    }

    /// <summary>The DbContext is owned and disposed by the DI scope, so there is nothing to release here.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
