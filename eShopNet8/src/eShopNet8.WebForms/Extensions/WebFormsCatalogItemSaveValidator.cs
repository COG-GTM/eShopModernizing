using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Infrastructure.Data;

namespace eShopNet8.WebForms.Extensions;

// SQL mode: the save-time EF6 validation of eShopLegacyWebForms.Models.CatalogItem. Unlike the MVC model it has no
// [Required] on Name and a wider Price range, so empty names are stored while out-of-range stock values fail (-> 500).
public sealed class WebFormsCatalogItemSaveValidator : ICatalogItemSaveValidator
{
    public void Validate(CatalogItem catalogItem)
    {
        var rules = new Rules
        {
            Price = catalogItem.Price,
            AvailableStock = catalogItem.AvailableStock,
            RestockThreshold = catalogItem.RestockThreshold,
            MaxStockThreshold = catalogItem.MaxStockThreshold,
        };
        Validator.ValidateObject(rules, new ValidationContext(rules), validateAllProperties: true);
    }

    // Annotations copied verbatim from eShopLegacyWebForms.Models.CatalogItem.
    private sealed class Rules
    {
        [RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage = "The field Price must be a positive number with maximum two decimals.")]
        [Range(0, 9999999999999999.99)]
        public decimal Price { get; init; }

        [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
        public int AvailableStock { get; init; }

        [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
        public int RestockThreshold { get; init; }

        [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
        public int MaxStockThreshold { get; init; }
    }
}
