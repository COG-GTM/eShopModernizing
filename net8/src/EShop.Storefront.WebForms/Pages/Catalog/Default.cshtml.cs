using EShop.Catalog.Contracts;
using EShop.Storefront.WebForms.Catalog;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EShop.Storefront.WebForms.Pages.Catalog;

/// <summary>Legacy Default.aspx: paged product list (default size 10, index 0).</summary>
public class DefaultModel(ICatalogService catalogService, ILogger<DefaultModel> logger) : PageModel
{
    public const int DefaultPageIndex = 0;
    public const int DefaultPageSize = 10;

    public PaginatedItemsViewModel<CatalogItem> Model { get; private set; } = default!;

    public void OnGet()
    {
        var size = DefaultPageSize;
        var index = DefaultPageIndex;
        if (RouteData.Values.ContainsKey("size") && RouteData.Values.ContainsKey("index"))
        {
            size = WebFormsCatalogForm.RouteInt(RouteData.Values["size"]);
            index = WebFormsCatalogForm.RouteInt(RouteData.Values["index"]);
        }

        Model = catalogService.GetCatalogItemsPaginated(size, index);
        logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", size, index);
    }

    public string PageUrl(int index) => Url.Content($"~/Default/index/{index}/size/{Model.ItemsPerPage}");
}
