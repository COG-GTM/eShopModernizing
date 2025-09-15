using eShopCoreAPI.Data;
using eShopCoreAPI.Models;
using eShopCoreAPI.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreAPI.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _context;
    private bool _disposed = false;

    public CatalogService(CatalogDbContext context)
    {
        _context = context;
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _context.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(c => c.Id == id);
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _context.CatalogBrands.ToList();
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _context.CatalogItems.LongCount();

        var itemsOnPage = _context.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Count = totalItems,
            Data = itemsOnPage
        };
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _context.CatalogTypes.ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        _context.CatalogItems.Add(catalogItem);
        _context.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        _context.CatalogItems.Update(catalogItem);
        _context.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        _context.CatalogItems.Remove(catalogItem);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Dispose();
        }
        _disposed = true;
    }
}
