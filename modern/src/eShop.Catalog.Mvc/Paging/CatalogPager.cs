using eShop.Catalog.Data.ViewModel;

namespace eShop.Catalog.Mvc.Paging;

/// <summary>Pager rules of the legacy Views/Catalog/Index.cshtml: both links are always rendered, out-of-range ones get the hidden class.</summary>
public static class CatalogPager
{
    public const string NavigableClass = "esh-pager-item esh-pager-item--navigable";
    public const string HiddenClass = "esh-pager-item--hidden";

    public static bool HasPrevious<T>(PaginatedItemsViewModel<T> model)
        where T : class => model.ActualPage > 0;

    public static bool HasNext<T>(PaginatedItemsViewModel<T> model)
        where T : class => model.ActualPage < model.TotalPages - 1;

    public static string PreviousCssClass<T>(PaginatedItemsViewModel<T> model)
        where T : class => CssClass(HasPrevious(model));

    public static string NextCssClass<T>(PaginatedItemsViewModel<T> model)
        where T : class => CssClass(HasNext(model));

    public static object PreviousRouteValues<T>(PaginatedItemsViewModel<T> model)
        where T : class => new { pageSize = model.ItemsPerPage, pageIndex = model.ActualPage - 1 };

    public static object NextRouteValues<T>(PaginatedItemsViewModel<T> model)
        where T : class => new { pageSize = model.ItemsPerPage, pageIndex = model.ActualPage + 1 };

    public static string Summary<T>(PaginatedItemsViewModel<T> model)
        where T : class =>
        $"Showing {model.ItemsPerPage} of {model.TotalItems} products - Page {model.ActualPage + 1} - {model.TotalPages}";

    private static string CssClass(bool navigable) => navigable ? NavigableClass : NavigableClass + " " + HiddenClass;
}
