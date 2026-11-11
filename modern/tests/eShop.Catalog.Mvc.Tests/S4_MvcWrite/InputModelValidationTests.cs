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
    private const string NameLength = "The field Name must be a string or array type with a maximum length of '50'.";

    public static TheoryData<string, object?, string?> BoundaryTable => new()
    {
        // property, value, expected error (null = valid)
        { "Name", "x", null },
        { "Name", null, "The Name field is required." },
        { "Name", "   ", "The Name field is required." },
        // Catalog.Name is nvarchar(50): over-long names re-render instead of failing in the database.
        { "Name", new string('A', 50), null },
        { "Name", new string('A', 51), NameLength },
        { "Name", new string('\u00e9', 50), null },
        { "Name", new string('\u00e9', 51), NameLength },
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
        // Beyond Int32: legacy RangeAttribute threw OverflowException (500); now a range message.
        { "Price", 2147483648m, PriceRange },
        { "Price", 99999999999999999999.99m, PriceRange },
        { "Price", decimal.MaxValue, PriceRange },
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

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("12345", true)]
    [InlineData("123456", false)]
    [InlineData(123456, true)]
    public void Column_max_length_only_limits_strings(object? value, bool expected)
    {
        Assert.Equal(expected, new ColumnMaxLengthAttribute(5).IsValid(value));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1000000", true)]
    [InlineData("1000001", false)]
    [InlineData("-1", false)]
    [InlineData("2147483648", false)]
    [InlineData("-2147483649", false)]
    [InlineData("79228162514264337593543950335", false)]
    public void Safe_range_rejects_values_beyond_int32_instead_of_throwing(string value, bool expected)
    {
        var attribute = new SafeRangeAttribute(0, 1000000);

        Assert.Equal(expected, attribute.IsValid(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("The field Price must be between 0 and 1000000.", attribute.FormatErrorMessage("Price"));
    }

    [Fact]
    public void Name_and_price_limits_use_the_server_side_attributes()
    {
        var name = typeof(CatalogItemInputModel).GetProperty(nameof(CatalogItemInputModel.Name))!;
        var price = typeof(CatalogItemInputModel).GetProperty(nameof(CatalogItemInputModel.Price))!;

        Assert.Equal(50, Assert.Single(name.GetCustomAttributes(typeof(ColumnMaxLengthAttribute), false).Cast<ColumnMaxLengthAttribute>()).Length);
        Assert.Empty(name.GetCustomAttributes(typeof(StringLengthAttribute), false));
        Assert.Empty(name.GetCustomAttributes(typeof(MaxLengthAttribute), false));
        Assert.Single(price.GetCustomAttributes(typeof(SafeRangeAttribute), false));
    }
}
