using EShop.Catalog.Contracts;
using EShop.Storefront.WebForms.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EShop.Storefront.WebForms.Pages.Catalog;

/// <summary>Legacy Catalog/Delete.aspx: confirmation page; the "[ Delete ]" postback removes the item and redirects to /.</summary>
public class DeleteModel(ICatalogService catalogService, ILogger<DeleteModel> logger) : PageModel
{
    public const string DeleteButtonId = "ctl11";

    public CatalogItem ProductToDelete { get; private set; } = default!;

    public void OnGet() => LoadProduct();

    public IActionResult OnPost()
    {
        LoadProduct();
        if (!WebFormsCatalogForm.WasClicked(Request.Form, DeleteButtonId))
        {
            return Page();
        }

        catalogService.RemoveCatalogItem(ProductToDelete);
        return Redirect(Url.Content("~/"));
    }

    private void LoadProduct()
    {
        var productId = WebFormsCatalogForm.RouteInt(RouteData.Values["id"]);
        logger.LogInformation("Now loading... /Catalog/Delete.aspx?id={ProductId}", productId);
        ProductToDelete = catalogService.FindCatalogItem(productId) ?? throw new CatalogItemNotFoundException(productId);
    }
}
