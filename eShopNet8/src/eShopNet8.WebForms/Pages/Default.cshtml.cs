using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages;

// Port of Default.aspx(.cs): paginated catalog list.
public class DefaultModel : PageModel
{
    public const int DefaultPageIndex = 0;
    public const int DefaultPageSize = 10;

    private readonly ICatalogService _catalogService;
    private readonly ILogger<DefaultModel> _logger;

    public DefaultModel(ICatalogService catalogService, ILogger<DefaultModel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public PaginatedItemsViewModel<CatalogItem> Model { get; private set; } = null!;

    public void OnGet()
    {
        if (RouteData.Values.ContainsKey("size") && RouteData.Values.ContainsKey("index"))
        {
            // Convert.ToInt32 like the legacy page: non-numeric route values throw (500, quirk Q5).
            var size = Convert.ToInt32(RouteData.Values["size"]);
            var index = Convert.ToInt32(RouteData.Values["index"]);
            Model = _catalogService.GetCatalogItemsPaginated(size, index);
            _logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", size, index);
        }
        else
        {
            Model = _catalogService.GetCatalogItemsPaginated(DefaultPageSize, DefaultPageIndex);
            _logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", DefaultPageSize, DefaultPageIndex);
        }
    }

    public string PageUrl(int index) => $"/Default/index/{index}/size/{Model.ItemsPerPage}";

    public string NextCssClass =>
        "esh-pager-item esh-pager-item--navigable" + (Model.ActualPage < Model.TotalPages - 1 ? "" : " esh-pager-item--hidden");

    public string PreviousCssClass =>
        "esh-pager-item esh-pager-item--navigable" + (Model.ActualPage > 0 ? "" : " esh-pager-item--hidden");
}
