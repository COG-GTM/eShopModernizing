using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Mvc.Models;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

public class InputModelValidationTests
{
    private static CatalogItemInputModel Valid() => new()
    {
        Id = 1,
        Name = "Mug",
        Description = "A mug",
        Price = 19.50m,
        CatalogBrandId = 2,
        CatalogTypeId = 1,
        AvailableStock = 100,
        RestockThreshold = 10,
        MaxStockThreshold = 200,
    };

    private const string PriceRegex = "The field Price must be a positive number with maximum two decimals.";
    private const string PriceRange = "The field Price must be between 0 and 1000000.";

    public static TheoryData<string, object?, string?> BoundaryTable => new()
    {
        // property, value, expected error (null = valid)
        { "Name", "x", null },
        { "Name", null, "The Name field is required." },
        { "Price", 0m, null },
        { "Price", 0.01m, null },
        { "Price", 12.5m, null },
        { "Price", 12.55m, null },
        { "Price", 12.555m, PriceRegex },
        { "Price", -1m, PriceRegex },
        { "Price", 1000000m, null },
        // Range(int, int) converts with Convert.ToInt32 (rounds), exactly like legacy: 1000000.01 passes.
        { "Price", 1000000.01m, null },
        { "Price", 1000000.5m, null },
        { "Price", 1000001m, PriceRange },
        { "Price", null, "The Price field is required." },
        { "AvailableStock", -1, "The field Stock must be between 0 and 10 million." },
        { "AvailableStock", 0, null },
        { "AvailableStock", 10000000, null },
        { "AvailableStock", 10000001, "The field Stock must be between 0 and 10 million." },
        { "AvailableStock", null, "The Stock field is required." },
        { "RestockThreshold", -1, "The field Restock must be between 0 and 10 million." },
        { "RestockThreshold", 0, null },
        { "RestockThreshold", 10000000, null },
        { "RestockThreshold", 10000001, "The field Restock must be between 0 and 10 million." },
        { "MaxStockThreshold", -1, "The field Max stock must be between 0 and 10 million." },
        { "MaxStockThreshold", 0, null },
        { "MaxStockThreshold", 10000000, null },
        { "MaxStockThreshold", 10000001, "The field Max stock must be between 0 and 10 million." },
        { "CatalogBrandId", null, "The Brand field is required." },
        { "CatalogTypeId", null, "The Type field is required." },
    };

    [Theory]
    [MemberData(nameof(BoundaryTable))]
    public void Validation_attribute_boundaries_match_legacy_messages(string property, object? value, string? expectedError)
    {
        var model = Valid();
        typeof(CatalogItemInputModel).GetProperty(property)!.SetValue(model, value);

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        if (expectedError == null)
        {
            Assert.True(valid, string.Join(" | ", results.Select(r => r.ErrorMessage)));
        }
        else
        {
            // MVC shows the first error per field; attributes run in declaration order (Required, Regex, Range).
            Assert.All(results, r => Assert.Equal(new[] { property }, r.MemberNames));
            Assert.Equal(expectedError, results[0].ErrorMessage);
        }
    }
}
