using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>Port of Catalog/Create.aspx (route <c>/Catalog/Create</c>).</summary>
public class CreateModel : CatalogItemFormPageModel
{
    public CreateModel(ICatalogService catalogService)
        : base(catalogService)
    {
    }

    // Empty prefix: fields bind only by their legacy names, even when the form also carries "Input.*" keys.
    [BindProperty(Name = "")]
    public CreateCatalogItemInput Input { get; set; } = new();

    public void OnGet()
    {
        LoadLookups();
    }

    public IActionResult OnPost()
    {
        LoadLookups();
        var (brandId, typeId) = ResolveLookups(Input);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        CatalogService.CreateCatalogItem(Input.ToNewCatalogItem(brandId, typeId));
        return Redirect(Url.Content("~/"));
    }
}
