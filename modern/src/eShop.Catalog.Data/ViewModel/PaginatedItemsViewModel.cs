namespace eShop.Catalog.Data.ViewModel;

public class PaginatedItemsViewModel<TEntity>
    where TEntity : class
{
    public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    {
        ActualPage = pageIndex;
        ItemsPerPage = pageSize;
        TotalItems = count;
        TotalPages = (int)Math.Ceiling((decimal)count / pageSize);
        Data = data;
    }

    public int ActualPage { get; private set; }

    public int ItemsPerPage { get; private set; }

    public long TotalItems { get; private set; }

    public int TotalPages { get; set; }

    public IEnumerable<TEntity> Data { get; private set; }
}
