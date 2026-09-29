using System.ComponentModel.DataAnnotations;
using System.Globalization;
using eShopRazorPages.Models;

namespace eShopRazorPages.ViewModel;

/// <summary>
/// Form model for the Create and Edit pages. Numeric fields are bound as strings, like the legacy
/// <c>asp:TextBox</c> controls, so every invalid entry reports the legacy validator message instead of a
/// generic model-binding error.
/// </summary>
public class CatalogItemInput
{
    public const string PriceErrorMessage = "The Price must be a positive number with maximum two decimals between 0 and 1 million.";
    public const string StockErrorMessage = "The field Stock must be between 0 and 10 million.";
    public const string RestockErrorMessage = "The field Restock must be between 0 and 10 million.";
    public const string MaxStockErrorMessage = "The field Max stock must be between 0 and 10 million.";

    private const string IntegerPattern = @"\d+";
    private const string CurrencyPattern = @"\d+(\.\d{1,2})?";

    [Required(ErrorMessage = "The Name field is required.")]
    [StringLength(50, ErrorMessage = "The Name field must be 50 characters or fewer.")]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Display(Name = "Brand")]
    public int CatalogBrandId { get; set; }

    [Display(Name = "Type")]
    public int CatalogTypeId { get; set; }

    [Required(ErrorMessage = PriceErrorMessage)]
    [RegularExpression(CurrencyPattern, ErrorMessage = PriceErrorMessage)]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = PriceErrorMessage)]
    public string? Price { get; set; } = "0.00";

    [Required(ErrorMessage = StockErrorMessage)]
    [RegularExpression(IntegerPattern, ErrorMessage = StockErrorMessage)]
    [Range(0, 10000000, ErrorMessage = StockErrorMessage)]
    public string? Stock { get; set; } = "0";

    [Required(ErrorMessage = RestockErrorMessage)]
    [RegularExpression(IntegerPattern, ErrorMessage = RestockErrorMessage)]
    [Range(0, 10000000, ErrorMessage = RestockErrorMessage)]
    public string? Restock { get; set; } = "0";

    [Required(ErrorMessage = MaxStockErrorMessage)]
    [RegularExpression(IntegerPattern, ErrorMessage = MaxStockErrorMessage)]
    [Range(0, 10000000, ErrorMessage = MaxStockErrorMessage)]
    public string? Maxstock { get; set; } = "0";

    public static CatalogItemInput FromCatalogItem(CatalogItem item) => new()
    {
        Name = item.Name,
        Description = item.Description,
        CatalogBrandId = item.CatalogBrandId,
        CatalogTypeId = item.CatalogTypeId,
        Price = item.Price.ToString(CultureInfo.InvariantCulture),
        Stock = item.AvailableStock.ToString(CultureInfo.InvariantCulture),
        Restock = item.RestockThreshold.ToString(CultureInfo.InvariantCulture),
        Maxstock = item.MaxStockThreshold.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Copies the validated form values onto <paramref name="item"/>.</summary>
    public CatalogItem ApplyTo(CatalogItem item)
    {
        item.Name = Name!;
        item.Description = Description;
        item.CatalogBrandId = CatalogBrandId;
        item.CatalogTypeId = CatalogTypeId;
        item.Price = decimal.Parse(Price!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        item.AvailableStock = int.Parse(Stock!, NumberStyles.None, CultureInfo.InvariantCulture);
        item.RestockThreshold = int.Parse(Restock!, NumberStyles.None, CultureInfo.InvariantCulture);
        item.MaxStockThreshold = int.Parse(Maxstock!, NumberStyles.None, CultureInfo.InvariantCulture);
        return item;
    }
}
