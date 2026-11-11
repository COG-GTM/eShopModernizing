using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>Port of Catalog/Edit.aspx (route <c>/Catalog/Edit/{id}</c>).</summary>
public class EditModel : CatalogItemFormPageModel
{
    public EditModel(ICatalogService catalogService)
        : base(catalogService)
    {
    }

    // Empty prefix: fields bind only by their legacy names, even when the form also carries "Input.*" keys.
    [BindProperty(Name = "")]
    public EditCatalogItemInput Input { get; set; } = new();

    /// <summary>Read-only, never bound from the form: always the stored value.</summary>
    public string PictureFileName { get; private set; } = string.Empty;

    public void OnGet([FromRoute] int id)
    {
        var item = FindItem(id);
        Input = EditCatalogItemInput.From(item);
        PictureFileName = item.PictureFileName;
        LoadLookups();
    }

    public IActionResult OnPost([FromRoute] int id)
    {
        var item = FindItem(id);
        PictureFileName = item.PictureFileName;
        LoadLookups();
        var (brandId, typeId) = ResolveLookups(Input);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Input.ApplyTo(item, brandId, typeId);
        CatalogService.UpdateCatalogItem(item);
        return Redirect(Url.Content("~/"));
    }

    // Legacy Edit.aspx dereferences the missing product (NullReferenceException -> 500); keep the 500.
    private CatalogItem FindItem(int id)
    {
        return CatalogService.FindCatalogItem(id) ?? throw new KeyNotFoundException($"Catalog item {id} was not found.");
    }
}
