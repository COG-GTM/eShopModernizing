namespace eShop.Catalog.Core.ViewModel;

public class PaginatedItemsViewModel<TEntity> where TEntity : class
{
    public int ActualPage { get; private set; }
    public int ItemsPerPage { get; private set; }
    public long TotalItems { get; private set; }
    public int TotalPages { get; set; }
    public IEnumerable<TEntity> Data { get; private set; }

    public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    {
        ActualPage = pageIndex;
        ItemsPerPage = pageSize;
        TotalItems = count;
        // Legacy semantics preserved: pageSize 0 throws DivideByZeroException (flow mvc-index-size0 -> 500, quirk Q5).
        TotalPages = (int)Math.Ceiling((decimal)count / pageSize);
        Data = data;
    }
}
