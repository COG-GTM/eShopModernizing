using eShopPortedWebForms.Models;
using eShopPortedWebForms.Services;
using eShopPortedWebForms.ViewModel;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopPortedWebForms.Pages;

public class IndexModel : PageModel
{
    private const int DefaultPageSize = 10;

    private readonly ICatalogService catalogService;

    public IndexModel(ICatalogService catalogService)
    {
        this.catalogService = catalogService;
    }

    public PaginatedItemsViewModel<CatalogItem> Catalog { get; private set; } = default!;

    public async Task OnGetAsync(int pageIndex = 0)
    {
        if (pageIndex < 0)
        {
            pageIndex = 0;
        }

        Catalog = await catalogService.GetCatalogItemsPaginatedAsync(DefaultPageSize, pageIndex);
    }
}
