using System.ComponentModel.DataAnnotations;
using eShop.Catalog.WebForms.Pages.Catalog;

namespace eShop.Catalog.WebForms.Tests.S8_PagesWrite;

public class ValidationBoundaryTests
{
    public static TheoryData<string, string?, string?> Boundaries => new()
    {
        // field, posted text, expected legacy validator message (null = valid)
        { "Name", "Mug", null },
        { "Name", "", CatalogItemFormInput.NameRequiredMessage },
        { "Name", "   ", CatalogItemFormInput.NameRequiredMessage },
        { "Name", null, CatalogItemFormInput.NameRequiredMessage },
        { "Price", "0", null },
        { "Price", "0.00", null },
        { "Price", "19.5", null },
        { "Price", "1,000.25", null },
        { "Price", "1000000", null },
        { "Price", "1000000.00", null },
        { "Price", "1000000.01", CatalogItemFormInput.PriceRangeMessage },
        { "Price", "-0.01", CatalogItemFormInput.PriceRangeMessage },
        { "Price", "1.234", CatalogItemFormInput.PriceRangeMessage },
        { "Price", "abc", CatalogItemFormInput.PriceRangeMessage },
        { "Price", "", null },
        { "Stock", "0", null },
        { "Stock", "10000000", null },
        { "Stock", "10000001", CatalogItemFormInput.StockRangeMessage },
        { "Stock", "-1", CatalogItemFormInput.StockRangeMessage },
        { "Stock", "1.5", CatalogItemFormInput.StockRangeMessage },
        { "Stock", "abc", CatalogItemFormInput.StockRangeMessage },
        { "Restock", "0", null },
        { "Restock", "10000000", null },
        { "Restock", "10000001", CatalogItemFormInput.RestockRangeMessage },
        { "Restock", "-1", CatalogItemFormInput.RestockRangeMessage },
        { "Maxstock", "0", null },
        { "Maxstock", "10000000", null },
        { "Maxstock", "10000001", CatalogItemFormInput.MaxStockRangeMessage },
        { "Maxstock", "-1", CatalogItemFormInput.MaxStockRangeMessage },
    };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Create_input_reproduces_legacy_validators(string field, string? value, string? expectedMessage)
    {
        var input = ValidCreateInput();
        typeof(CreateCatalogItemInput).GetProperty(field)!.SetValue(input, value);

        AssertValidation(input, field, expectedMessage);
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Edit_input_reproduces_legacy_validators(string field, string? value, string? expectedMessage)
    {
        var input = new EditCatalogItemInput { Name = "Mug", Price = "1", Stock = "1", Restock = "1", Maxstock = "1", BrandDropDownList = "1", TypeDropDownList = "1" };
        typeof(EditCatalogItemInput).GetProperty(field)!.SetValue(input, value);

        AssertValidation(input, field, expectedMessage);
    }

    [Fact]
    public void Create_input_defaults_match_legacy_form()
    {
        var input = new CreateCatalogItemInput();

        Assert.Equal("0.00", input.Price);
        Assert.Equal("0", input.Stock);
        Assert.Equal("0", input.Restock);
        Assert.Equal("0", input.Maxstock);
    }

    internal static CreateCatalogItemInput ValidCreateInput()
    {
        return new CreateCatalogItemInput { Name = "Mug", Description = "d", Brand = "1", Type = "1", Price = "1", Stock = "1", Restock = "1", Maxstock = "1" };
    }

    private static void AssertValidation(object input, string field, string? expectedMessage)
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);

        if (expectedMessage is null)
        {
            Assert.True(valid, string.Join("; ", results.Select(r => r.ErrorMessage)));
        }
        else
        {
            var result = Assert.Single(results);
            Assert.Equal(expectedMessage, result.ErrorMessage);
            Assert.Equal(field, Assert.Single(result.MemberNames));
        }
    }
}
