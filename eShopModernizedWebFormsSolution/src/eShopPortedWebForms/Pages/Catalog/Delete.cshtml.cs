using eShopPortedWebForms.Models;
using eShopPortedWebForms.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopPortedWebForms.Pages.Catalog;

public class DeleteModel : PageModel
{
    private readonly ICatalogService catalogService;

    public DeleteModel(ICatalogService catalogService)
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

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var item = await catalogService.FindCatalogItemAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        await catalogService.RemoveCatalogItemAsync(item);
        return RedirectToPage("/Index");
    }
}
