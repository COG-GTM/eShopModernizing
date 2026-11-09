using EShop.Catalog.Contracts;
using EShop.Catalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EShop.Catalog.Services;

/// <summary>Legacy EF6 <c>CatalogService</c> (scoped, <c>UseMockData=false</c>) on EF Core 8 / SQL Server.</summary>
public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext db;
    private readonly CatalogItemHiLoGenerator indexGenerator;

    public CatalogService(CatalogDbContext db, CatalogItemHiLoGenerator indexGenerator)
    {
        this.db = db;
        this.indexGenerator = indexGenerator;
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

    public CatalogItem? FindCatalogItem(int id) =>
        db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(ci => ci.Id == id);

    public IEnumerable<CatalogType> GetCatalogTypes() => db.CatalogTypes.OrderBy(t => t.Id).ToList();

    public IEnumerable<CatalogBrand> GetCatalogBrands() => db.CatalogBrands.OrderBy(b => b.Id).ToList();

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        catalogItem.Id = indexGenerator.GetNextSequenceValue(db);
        db.CatalogItems.Add(catalogItem);
        db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        // Copy scalar values onto the tracked row so a posted instance (with or without navigations) never re-inserts brands/types
        // or collides with an instance loaded earlier in the same scope. Unknown ids fail like legacy (DbUpdateConcurrencyException).
        var tracked = db.CatalogItems.Find(catalogItem.Id);
        if (tracked == null)
        {
            db.Entry(catalogItem).State = EntityState.Modified;
        }
        else if (!ReferenceEquals(tracked, catalogItem))
        {
            db.Entry(tracked).CurrentValues.SetValues(catalogItem);
        }

        db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        ArgumentNullException.ThrowIfNull(catalogItem);
        var tracked = db.CatalogItems.Find(catalogItem.Id) ?? catalogItem;
        db.CatalogItems.Remove(tracked);
        db.SaveChanges();
    }
}
