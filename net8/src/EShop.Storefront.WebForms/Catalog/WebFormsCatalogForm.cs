using System.Globalization;
using System.Text.RegularExpressions;
using EShop.Catalog.Contracts;
using Microsoft.AspNetCore.Http;

namespace EShop.Storefront.WebForms.Catalog;

/// <summary>
/// Posted values of the legacy Create/Edit pages. Field names keep the WebForms naming container prefix
/// (<c>ctl00$MainContent$</c>) and the validators reproduce RequiredFieldValidator / RangeValidator semantics.
/// </summary>
public sealed partial class WebFormsCatalogForm
{
    public const string Prefix = "ctl00$MainContent$";

    public const string NameRequiredMessage = "The Name field is required.";
    public const string PriceRangeMessage = "The Price must be a positive number with maximum two decimals between 0 and 1 million.";
    public const string StockRangeMessage = "The field Stock must be between 0 and 10 million.";
    public const string RestockRangeMessage = "The field Restock must be between 0 and 10 million.";
    public const string MaxStockRangeMessage = "The field Max stock must be between 0 and 10 million.";

    /// <summary>Legacy pages run under en-US; decimal/int parsing and price rendering use it explicitly.</summary>
    public static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Type { get; set; }
    public string? Price { get; set; }
    public string? PictureFileName { get; set; }
    public string? Stock { get; set; }
    public string? Restock { get; set; }
    public string? Maxstock { get; set; }

    /// <summary>Validator messages keyed by field name; empty when every validator passes.</summary>
    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    public string? ErrorFor(string field) => Errors.TryGetValue(field, out var message) ? message : null;

    public static WebFormsCatalogForm Empty() =>
        new() { Price = "0.00", Stock = "0", Restock = "0", Maxstock = "0" };

    public static WebFormsCatalogForm FromItem(CatalogItem item) => new()
    {
        Name = item.Name,
        Description = item.Description,
        Brand = item.CatalogBrandId.ToString(CultureInfo.InvariantCulture),
        Type = item.CatalogTypeId.ToString(CultureInfo.InvariantCulture),
        Price = FormatPrice(item.Price),
        PictureFileName = item.PictureFileName,
        Stock = item.AvailableStock.ToString(LegacyCulture),
        Restock = item.RestockThreshold.ToString(LegacyCulture),
        Maxstock = item.MaxStockThreshold.ToString(LegacyCulture),
    };

    public static WebFormsCatalogForm FromRequest(IFormCollection form, string brandField, string typeField) => new()
    {
        Name = Value(form, "Name"),
        Description = Value(form, "Description"),
        Brand = Value(form, brandField),
        Type = Value(form, typeField),
        Price = Value(form, "Price"),
        PictureFileName = Value(form, "PictureFileName"),
        Stock = Value(form, "Stock"),
        Restock = Value(form, "Restock"),
        Maxstock = Value(form, "Maxstock"),
    };

    /// <summary>True when the named WebForms submit button is part of the post (i.e. its Click event would fire).</summary>
    public static bool WasClicked(IFormCollection form, string buttonId) => form.ContainsKey(Prefix + buttonId);

    /// <summary>Legacy <c>&lt;%#: Item.Price %&gt;</c>: decimal.ToString() keeps the stored scale (19.5, 8.50, 12).</summary>
    public static string FormatPrice(decimal price) => price.ToString(LegacyCulture);

    /// <summary>Legacy <c>Convert.ToInt32(Page.RouteData.Values[key])</c>: null is 0, non-numeric throws FormatException (500).</summary>
    public static int RouteInt(object? value) => Convert.ToInt32(value as string, LegacyCulture);

    /// <summary>
    /// A DropDownList ignores a posted value that is not one of its items and keeps its previous selection,
    /// so an unknown brand/type id falls back to <paramref name="fallbackBrand"/>/<paramref name="fallbackType"/>.
    /// </summary>
    public void ConstrainSelections(IEnumerable<CatalogBrand> brands, IEnumerable<CatalogType> types, string? fallbackBrand, string? fallbackType)
    {
        if (!brands.Any(b => b.Id.ToString(CultureInfo.InvariantCulture) == Brand))
        {
            Brand = fallbackBrand;
        }
        if (!types.Any(t => t.Id.ToString(CultureInfo.InvariantCulture) == Type))
        {
            Type = fallbackType;
        }
    }

    public bool Validate()
    {
        Errors.Clear();
        if (string.IsNullOrWhiteSpace(Name))
        {
            Errors["Name"] = NameRequiredMessage;
        }
        if (!CurrencyInRange(Price, 0m, 1_000_000m))
        {
            Errors["Price"] = PriceRangeMessage;
        }
        if (!IntegerInRange(Stock, 0, 10_000_000))
        {
            Errors["Stock"] = StockRangeMessage;
        }
        if (!IntegerInRange(Restock, 0, 10_000_000))
        {
            Errors["Restock"] = RestockRangeMessage;
        }
        if (!IntegerInRange(Maxstock, 0, 10_000_000))
        {
            Errors["Maxstock"] = MaxStockRangeMessage;
        }
        return Errors.Count == 0;
    }

    /// <summary>Legacy code-behind conversion: int.Parse / decimal.Parse of the TextBox text (throws on bad input like legacy).</summary>
    public CatalogItem ToCatalogItem(int id = 0) => new()
    {
        Id = id,
        Name = Name ?? string.Empty,
        Description = Description,
        CatalogBrandId = int.Parse(Brand!, LegacyCulture),
        CatalogTypeId = int.Parse(Type!, LegacyCulture),
        Price = decimal.Parse(Price!, LegacyCulture),
        PictureFileName = PictureFileName ?? CatalogItem.DefaultPictureName,
        AvailableStock = int.Parse(Stock!, LegacyCulture),
        RestockThreshold = int.Parse(Restock!, LegacyCulture),
        MaxStockThreshold = int.Parse(Maxstock!, LegacyCulture),
    };

    private static string? Value(IFormCollection form, string field) =>
        form.TryGetValue(Prefix + field, out var v) ? v.ToString() : null;

    // RangeValidator: an empty (after trim) input is valid; a value that fails the type conversion is invalid.
    private static bool CurrencyInRange(string? text, decimal min, decimal max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var m = CurrencyRegex().Match(text);
        if (!m.Success)
        {
            return false;
        }
        var number = m.Groups["sign"].Value + m.Groups["int"].Value.Replace(",", string.Empty, StringComparison.Ordinal)
                     + m.Groups["whole"].Value + (m.Groups["dec"].Success ? "." + m.Groups["dec"].Value : string.Empty);
        return decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
               && value >= min && value <= max;
    }

    private static bool IntegerInRange(string? text, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var m = IntegerRegex().Match(text);
        return m.Success
               && int.TryParse(m.Groups["sign"].Value + m.Groups["digits"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
               && value >= min && value <= max;
    }

    // BaseCompareValidator currency expression for en-US (group ',', decimal '.', 2 currency digits).
    [GeneratedRegex(@"^\s*(?<sign>[-+])?(?<int>(\d+,)*)(?<whole>\d+)(\.(?<dec>\d{1,2}))?\s*$")]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex(@"^\s*(?<sign>[-+])?(?<digits>\d+)\s*$")]
    private static partial Regex IntegerRegex();
}
