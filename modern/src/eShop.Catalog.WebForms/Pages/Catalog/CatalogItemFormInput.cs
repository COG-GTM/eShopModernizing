using System.ComponentModel.DataAnnotations;
using System.Globalization;
using eShop.Catalog.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>
/// Input model shared by Create.aspx / Edit.aspx ports. Form field names are the legacy WebForms names
/// (<c>ctl00$MainContent$Name</c>, ...), validation reproduces the legacy RequiredField/Range validators and their
/// messages, and only the fields the legacy pages post are bindable (no PictureFileName, OnReorder or Id).
/// </summary>
public abstract class CatalogItemFormInput
{
    public const string FieldNamePrefix = "ctl00$MainContent$";
    public const string ClientIdPrefix = "ctl00_MainContent_";

    public const string NameRequiredMessage = "The Name field is required.";
    public const string PriceRangeMessage = "The Price must be a positive number with maximum two decimals between 0 and 1 million.";
    public const string StockRangeMessage = "The field Stock must be between 0 and 10 million.";
    public const string RestockRangeMessage = "The field Restock must be between 0 and 10 million.";
    public const string MaxStockRangeMessage = "The field Max stock must be between 0 and 10 million.";

    public const string PriceMinimum = "0";
    public const string PriceMaximum = "1000000";
    public const string StockMinimum = "0";
    public const string StockMaximum = "10000000";

    internal static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    [ModelBinder(Name = FieldNamePrefix + nameof(Name))]
    [Required(ErrorMessage = NameRequiredMessage)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Name { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Description))]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Description { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Price))]
    [WebFormsRange(WebFormsValidationDataType.Currency, PriceMinimum, PriceMaximum, ErrorMessage = PriceRangeMessage)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Price { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Stock))]
    [WebFormsRange(WebFormsValidationDataType.Integer, StockMinimum, StockMaximum, ErrorMessage = StockRangeMessage)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Stock { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Restock))]
    [WebFormsRange(WebFormsValidationDataType.Integer, StockMinimum, StockMaximum, ErrorMessage = RestockRangeMessage)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Restock { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Maxstock))]
    [WebFormsRange(WebFormsValidationDataType.Integer, StockMinimum, StockMaximum, ErrorMessage = MaxStockRangeMessage)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Maxstock { get; set; }

    /// <summary>Posted value of the brand drop-down (field name differs between Create and Edit).</summary>
    public abstract string? SelectedBrand { get; }

    /// <summary>Posted value of the type drop-down (field name differs between Create and Edit).</summary>
    public abstract string? SelectedType { get; }

    /// <summary>Legacy <c>Create_Click</c>: a new item with the posted values (PictureFileName stays the default).</summary>
    public CatalogItem ToNewCatalogItem(int brandId, int typeId)
    {
        var item = new CatalogItem();
        ApplyTo(item, brandId, typeId);
        return item;
    }

    /// <summary>
    /// Load-then-patch: copies only the posted fields onto <paramref name="item"/>. Parsing uses the legacy
    /// <c>decimal.Parse</c>/<c>int.Parse</c> calls and happens before any assignment, so a parse failure changes nothing.
    /// </summary>
    public void ApplyTo(CatalogItem item, int brandId, int typeId)
    {
        var price = decimal.Parse(Price ?? string.Empty, NumberStyles.Number, LegacyCulture);
        var stock = int.Parse(Stock ?? string.Empty, NumberStyles.Integer, LegacyCulture);
        var restock = int.Parse(Restock ?? string.Empty, NumberStyles.Integer, LegacyCulture);
        var maxStock = int.Parse(Maxstock ?? string.Empty, NumberStyles.Integer, LegacyCulture);

        item.Name = Name ?? string.Empty;
        item.Description = Description ?? string.Empty;
        item.CatalogBrandId = brandId;
        item.CatalogTypeId = typeId;
        item.Price = price;
        item.AvailableStock = stock;
        item.RestockThreshold = restock;
        item.MaxStockThreshold = maxStock;
    }

    protected void LoadFrom(CatalogItem item)
    {
        Name = item.Name;
        Description = item.Description;
        Price = item.Price.ToString(LegacyCulture);
        Stock = item.AvailableStock.ToString(LegacyCulture);
        Restock = item.RestockThreshold.ToString(LegacyCulture);
        Maxstock = item.MaxStockThreshold.ToString(LegacyCulture);
    }
}

/// <summary>Create.aspx: drop-downs are <c>Brand</c> / <c>Type</c>; Price defaults to 0.00 and stock fields to 0.</summary>
public sealed class CreateCatalogItemInput : CatalogItemFormInput
{
    public CreateCatalogItemInput()
    {
        Price = "0.00";
        Stock = "0";
        Restock = "0";
        Maxstock = "0";
    }

    [ModelBinder(Name = FieldNamePrefix + nameof(Brand))]
    public string? Brand { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(Type))]
    public string? Type { get; set; }

    public override string? SelectedBrand => Brand;

    public override string? SelectedType => Type;
}

/// <summary>Edit.aspx: drop-downs are <c>BrandDropDownList</c> / <c>TypeDropDownList</c>.</summary>
public sealed class EditCatalogItemInput : CatalogItemFormInput
{
    [ModelBinder(Name = FieldNamePrefix + nameof(BrandDropDownList))]
    public string? BrandDropDownList { get; set; }

    [ModelBinder(Name = FieldNamePrefix + nameof(TypeDropDownList))]
    public string? TypeDropDownList { get; set; }

    public override string? SelectedBrand => BrandDropDownList;

    public override string? SelectedType => TypeDropDownList;

    public static EditCatalogItemInput From(CatalogItem item)
    {
        var input = new EditCatalogItemInput
        {
            BrandDropDownList = item.CatalogBrandId.ToString(CultureInfo.InvariantCulture),
            TypeDropDownList = item.CatalogTypeId.ToString(CultureInfo.InvariantCulture),
        };
        input.LoadFrom(item);
        return input;
    }
}
