using System.Globalization;
using EShop.Catalog.Contracts;
using EShop.Storefront.WebForms.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EShop.Storefront.WebForms.Pages.Catalog;

/// <summary>Legacy Catalog/Edit.aspx: "[ Save ]" postback validates, updates the item and redirects to /.</summary>
public class EditModel(ICatalogService catalogService, ILogger<EditModel> logger) : PageModel
{
    public const string SaveButtonId = "ctl07";

    public int ProductId { get; private set; }
    public WebFormsCatalogForm Form { get; private set; } = WebFormsCatalogForm.Empty();
    public IReadOnlyList<CatalogBrand> Brands { get; private set; } = [];
    public IReadOnlyList<CatalogType> Types { get; private set; } = [];

    public void OnGet()
    {
        var product = LoadProduct();
        Form = WebFormsCatalogForm.FromItem(product);
    }

    public IActionResult OnPost()
    {
        var product = LoadProduct();
        Form = WebFormsCatalogForm.FromRequest(Request.Form, "BrandDropDownList", "TypeDropDownList");
        Form.ConstrainSelections(Brands, Types,
            product.CatalogBrandId.ToString(CultureInfo.InvariantCulture),
            product.CatalogTypeId.ToString(CultureInfo.InvariantCulture));
        // The picture TextBox is ReadOnly in legacy, so a posted value is ignored.
        Form.PictureFileName = product.PictureFileName;
        if (!WebFormsCatalogForm.WasClicked(Request.Form, SaveButtonId) || !Form.Validate())
        {
            return Page();
        }

        catalogService.UpdateCatalogItem(Form.ToCatalogItem(ProductId));
        return Redirect(Url.Content("~/"));
    }

    private CatalogItem LoadProduct()
    {
        ProductId = WebFormsCatalogForm.RouteInt(RouteData.Values["id"]);
        logger.LogInformation("Now loading... /Catalog/Edit.aspx?id={ProductId}", ProductId);
        Brands = catalogService.GetCatalogBrands().ToList();
        Types = catalogService.GetCatalogTypes().ToList();
        return catalogService.FindCatalogItem(ProductId) ?? throw new CatalogItemNotFoundException(ProductId);
    }
}
