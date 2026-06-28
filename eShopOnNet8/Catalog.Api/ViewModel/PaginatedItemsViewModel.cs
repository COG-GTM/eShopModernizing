namespace Catalog.Api.ViewModel;

// Ported from eShopLegacyMVCSolution/src/eShopLegacyMVC/ViewModel/PaginatedItemsViewModel.cs
public class PaginatedItemsViewModel<TEntity> where TEntity : class
{
    public int ActualPage { get; private set; }
    public int ItemsPerPage { get; private set; }
    public long TotalItems { get; private set; }
    public int TotalPages { get; private set; }
    public IEnumerable<TEntity> Data { get; private set; }

    public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    {
        ActualPage = pageIndex;
        ItemsPerPage = pageSize;
        TotalItems = count;
        TotalPages = (int)Math.Ceiling((decimal)count / pageSize);
        Data = data;
    }
}
