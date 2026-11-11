using System.Globalization;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using eShop.Catalog.Data.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShop.Catalog.WebForms.Pages;

/// <summary>Port of legacy Default.aspx: catalog ListView + pager.</summary>
public class IndexModel : PageModel
{
    public const int DefaultPageIndex = 0;
    public const int DefaultPageSize = 10;

    private const string PagerItemCssClass = "esh-pager-item esh-pager-item--navigable";
    private const string PagerHiddenCssClass = " esh-pager-item--hidden";

    private readonly ICatalogService catalogService;
    private readonly ILogger<IndexModel> logger;

    public IndexModel(ICatalogService catalogService, ILogger<IndexModel> logger)
    {
        this.catalogService = catalogService;
        this.logger = logger;
    }

    public PaginatedItemsViewModel<CatalogItem> Catalog { get; private set; } = null!;

    public string PreviousUrl => ProductsByPageUrl(Catalog.ActualPage - 1, Catalog.ItemsPerPage);

    public string NextUrl => ProductsByPageUrl(Catalog.ActualPage + 1, Catalog.ItemsPerPage);

    public string PreviousCssClass => PagerItemCssClass + (Catalog.ActualPage > 0 ? string.Empty : PagerHiddenCssClass);

    public string NextCssClass => PagerItemCssClass + (Catalog.ActualPage < Catalog.TotalPages - 1 ? string.Empty : PagerHiddenCssClass);

    public static string ProductsByPageUrl(int index, int size) =>
        string.Create(CultureInfo.InvariantCulture, $"/Default/index/{index}/size/{size}");

    /// <summary>Route values only (legacy reads Page.RouteData); the query string is ignored like in legacy.</summary>
    public void OnGet([FromRoute] string? index, [FromRoute] string? size)
    {
        var pageSize = DefaultPageSize;
        var pageIndex = DefaultPageIndex;
        if (index is not null && size is not null)
        {
            pageSize = Convert.ToInt32(size, CultureInfo.InvariantCulture);
            pageIndex = Convert.ToInt32(index, CultureInfo.InvariantCulture);
        }

        Catalog = catalogService.GetCatalogItemsPaginated(pageSize, pageIndex);
        logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", pageSize, pageIndex);
    }
}
