using EShop.Catalog.Contracts;
using EShop.Storefront.WebForms.Catalog;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EShop.Storefront.WebForms.Pages.Catalog;

/// <summary>Legacy Catalog/Details.aspx. Missing or non-numeric id is a 500, as in legacy.</summary>
public class DetailsModel(ICatalogService catalogService, ILogger<DetailsModel> logger) : PageModel
{
    public CatalogItem Product { get; private set; } = default!;

    public void OnGet()
    {
        var productId = WebFormsCatalogForm.RouteInt(RouteData.Values["id"]);
        logger.LogInformation("Now loading... /Catalog/Details.aspx?id={ProductId}", productId);
        Product = catalogService.FindCatalogItem(productId) ?? throw new CatalogItemNotFoundException(productId);
    }
}
