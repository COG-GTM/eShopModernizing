using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShop.Catalog.Infrastructure.Data;

// Port of eShopLegacyMVC.Services.CatalogService (EF6). Semantics are kept on purpose (INVENTORY Q6/Q18):
// negative paging reaches SQL Server and fails, Update/Remove of an unknown item throw.
public class CatalogService : ICatalogService
{
    private readonly CatalogDBContext db;

    public CatalogService(CatalogDBContext db)
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

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return db.CatalogItems.Include(c => c.CatalogBrand).Include(c => c.CatalogType).FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return db.CatalogTypes.ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return db.CatalogBrands.ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        // Legacy always overwrote the id with the next catalog_hilo value; 0 makes EF Core's HiLo generator assign it.
        catalogItem.Id = 0;
        db.CatalogItems.Add(catalogItem);
        db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        db.Entry(catalogItem).State = EntityState.Modified;
        db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        db.CatalogItems.Remove(catalogItem);
        db.SaveChanges();
    }

    public void Dispose()
    {
        db.Dispose();
    }
}
