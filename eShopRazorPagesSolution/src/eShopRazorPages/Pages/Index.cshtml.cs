using System.Globalization;
using eShopRazorPages.Models;
using eShopRazorPages.Services;
using eShopRazorPages.ViewModel;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopRazorPages.Pages;

/// <summary>Port of <c>Default.aspx</c>: paginated catalog list.</summary>
public class IndexModel : PageModel
{
    public const int DefaultPageIndex = 0;
    public const int DefaultPageSize = 10;

    private readonly ICatalogService _catalogService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ICatalogService catalogService, ILogger<IndexModel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public PaginatedItemsViewModel<CatalogItem> Catalog { get; private set; } = default!;

    public bool HasPreviousPage => Catalog.ActualPage > 0;

    public bool HasNextPage => Catalog.ActualPage < Catalog.TotalPages - 1;

    public void OnGet()
    {
        var size = DefaultPageSize;
        var index = DefaultPageIndex;

        // Like the WebForms page, pagination comes only from route data (Default/index/{index}/size/{size}),
        // never from the query string.
        if (TryGetRouteInt("size", out var routeSize) && TryGetRouteInt("index", out var routeIndex))
        {
            size = routeSize;
            index = Math.Max(routeIndex, 0);
        }

        _logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", size, index);
        Catalog = _catalogService.GetCatalogItemsPaginated(size, index);
    }

    private bool TryGetRouteInt(string key, out int value)
    {
        value = 0;
        return RouteData.Values.TryGetValue(key, out var raw)
            && int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
