using eShopRazorPages.Models;
using eShopRazorPages.Services;
using eShopRazorPages.ViewModel;
using Microsoft.AspNetCore.Mvc;

namespace eShopRazorPages.Pages.Catalog;

/// <summary>Port of <c>Catalog/Edit.aspx</c>.</summary>
public class EditModel : CatalogFormPageModel
{
    private readonly ILogger<EditModel> _logger;

    public EditModel(ICatalogService catalogService, ILogger<EditModel> logger)
        : base(catalogService)
    {
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        _logger.LogInformation("Now loading... /Catalog/Edit.aspx?id={ProductId}", id);
        var product = CatalogService.FindCatalogItem(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        Input = CatalogItemInput.FromCatalogItem(product);
        LoadLookups();
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        var product = CatalogService.FindCatalogItem(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        LoadLookups();
        ValidateLookups();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The picture name is read-only in the UI, so it (and the non-editable reorder flag) is taken from the
        // stored item rather than from the posted form.
        var catalogItem = Input.ApplyTo(new CatalogItem
        {
            Id = id,
            PictureFileName = product.PictureFileName,
            PictureUri = product.PictureUri,
            OnReorder = product.OnReorder,
        });
        CatalogService.UpdateCatalogItem(catalogItem);
        return LocalRedirect("~/");
    }
}
