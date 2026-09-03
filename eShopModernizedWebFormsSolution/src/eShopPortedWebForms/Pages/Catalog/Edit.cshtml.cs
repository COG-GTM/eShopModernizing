using eShopPortedWebForms.Models;
using eShopPortedWebForms.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopPortedWebForms.Pages.Catalog;

public class EditModel : PageModel, ICatalogItemFormModel
{
    private readonly ICatalogService catalogService;

    public EditModel(ICatalogService catalogService)
    {
        this.catalogService = catalogService;
    }

    [BindProperty]
    public CatalogItem Item { get; set; } = new();

    public SelectList Brands { get; private set; } = default!;
    public SelectList Types { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await catalogService.FindCatalogItemAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        Item = item;
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Item.PictureFileName))
        {
            Item.PictureFileName = CatalogItem.DefaultPictureName;
        }

        await catalogService.UpdateCatalogItemAsync(Item);
        return RedirectToPage("/Index");
    }

    private async Task LoadSelectListsAsync()
    {
        Brands = new SelectList(await catalogService.GetCatalogBrandsAsync(), "Id", "Brand");
        Types = new SelectList(await catalogService.GetCatalogTypesAsync(), "Id", "Type");
    }
}
