using System.Globalization;
using EShop.Catalog.Contracts;
using EShop.Storefront.WebForms.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EShop.Storefront.WebForms.Pages.Catalog;

/// <summary>Legacy Catalog/Create.aspx: "[ Create ]" postback validates, creates the item and redirects to /.</summary>
public class CreateModel(ICatalogService catalogService) : PageModel
{
    public const string CreateButtonId = "ctl06";

    public WebFormsCatalogForm Form { get; private set; } = WebFormsCatalogForm.Empty();
    public IReadOnlyList<CatalogBrand> Brands { get; private set; } = [];
    public IReadOnlyList<CatalogType> Types { get; private set; } = [];

    public void OnGet() => LoadLists();

    public IActionResult OnPost()
    {
        LoadLists();
        Form = WebFormsCatalogForm.FromRequest(Request.Form, "Brand", "Type");
        Form.ConstrainSelections(Brands, Types,
            Brands.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture),
            Types.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture));
        if (!WebFormsCatalogForm.WasClicked(Request.Form, CreateButtonId) || !Form.Validate())
        {
            return Page();
        }

        var item = Form.ToCatalogItem();
        item.PictureFileName = CatalogItem.DefaultPictureName;
        catalogService.CreateCatalogItem(item);
        return Redirect(Url.Content("~/"));
    }

    private void LoadLists()
    {
        Brands = catalogService.GetCatalogBrands().ToList();
        Types = catalogService.GetCatalogTypes().ToList();
    }
}
