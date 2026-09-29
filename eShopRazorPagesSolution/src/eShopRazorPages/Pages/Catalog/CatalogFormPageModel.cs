using eShopRazorPages.Services;
using eShopRazorPages.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopRazorPages.Pages.Catalog;

/// <summary>Shared brand/type drop-down handling for the Create and Edit pages.</summary>
public abstract class CatalogFormPageModel : PageModel
{
    protected CatalogFormPageModel(ICatalogService catalogService)
    {
        CatalogService = catalogService;
    }

    [BindProperty]
    public CatalogItemInput Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Brands { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Types { get; private set; } = Array.Empty<SelectListItem>();

    protected ICatalogService CatalogService { get; }

    protected void LoadLookups()
    {
        Brands = CatalogService.GetCatalogBrands()
            .Select(b => new SelectListItem(b.Brand, b.Id.ToString()))
            .ToList();
        Types = CatalogService.GetCatalogTypes()
            .Select(t => new SelectListItem(t.Type, t.Id.ToString()))
            .ToList();
    }

    /// <summary>
    /// Rejects brand/type ids that are not offered in the drop-downs (WebForms event validation did this
    /// implicitly for <c>asp:DropDownList</c>).
    /// </summary>
    protected void ValidateLookups()
    {
        if (!Brands.Any(b => b.Value == Input.CatalogBrandId.ToString()))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CatalogBrandId)}", "Select a valid brand.");
        }

        if (!Types.Any(t => t.Value == Input.CatalogTypeId.ToString()))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CatalogTypeId)}", "Select a valid type.");
        }
    }
}
