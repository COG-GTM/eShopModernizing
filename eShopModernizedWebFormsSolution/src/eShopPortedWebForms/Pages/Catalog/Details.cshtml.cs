using eShopPortedWebForms.Models;
using eShopPortedWebForms.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopPortedWebForms.Pages.Catalog;

public class DetailsModel : PageModel
{
    private readonly ICatalogService catalogService;

    public DetailsModel(ICatalogService catalogService)
    {
        this.catalogService = catalogService;
    }

    public CatalogItem Item { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await catalogService.FindCatalogItemAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        Item = item;
        return Page();
    }
}
