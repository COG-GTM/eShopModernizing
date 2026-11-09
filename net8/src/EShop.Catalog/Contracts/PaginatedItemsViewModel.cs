// FROZEN shared contract. Mirrors legacy eShopLegacyMVC.ViewModel.PaginatedItemsViewModel<T>.
namespace EShop.Catalog.Contracts;

public class PaginatedItemsViewModel<TEntity> where TEntity : class
{
    public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    {
        ActualPage = pageIndex;
        ItemsPerPage = pageSize;
        TotalItems = count;
        TotalPages = (int)Math.Ceiling((decimal)count / pageSize); // pageSize 0 throws DivideByZeroException like legacy (500)
        Data = data;
    }

    public int ActualPage { get; }
    public int ItemsPerPage { get; }
    public long TotalItems { get; }
    public int TotalPages { get; }
    public IEnumerable<TEntity> Data { get; }
}
