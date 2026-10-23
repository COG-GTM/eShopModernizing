using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.ViewModel;
using Microsoft.Extensions.Configuration;

namespace eShop.Catalog.UnitTests;

public class CatalogItemValidationTests
{
    private static List<string?> Validate(CatalogItem item)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage).ToList();
    }

    private static CatalogItem Valid() => new() { Name = "n", Price = 1.5m, AvailableStock = 1, RestockThreshold = 1, MaxStockThreshold = 1 };

    [Fact]
    public void Valid_item_has_no_errors() => Assert.Empty(Validate(Valid()));

    [Theory]
    [InlineData(nameof(CatalogItem.AvailableStock), "The field Stock must be between 0 and 10 million.")]
    [InlineData(nameof(CatalogItem.RestockThreshold), "The field Restock must be between 0 and 10 million.")]
    [InlineData(nameof(CatalogItem.MaxStockThreshold), "The field Max stock must be between 0 and 10 million.")]
    public void Stock_range_messages_match_legacy(string property, string message)
    {
        var item = Valid();
        typeof(CatalogItem).GetProperty(property)!.SetValue(item, -1);
        Assert.Contains(message, Validate(item));
    }

    [Fact]
    public void Name_required_message_matches_legacy()
    {
        var item = Valid();
        item.Name = "";
        Assert.Equal(new[] { "The Name field is required." }, Validate(item));
    }

    [Theory]
    [InlineData("12.345")]
    [InlineData("1.234")]
    public void Price_with_more_than_two_decimals_uses_the_legacy_regex_message(string price)
    {
        var item = Valid();
        item.Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new[] { "The field Price must be a positive number with maximum two decimals." }, Validate(item));
    }

    [Fact]
    public void Price_above_range_uses_the_default_range_message()
    {
        var item = Valid();
        item.Price = 1000001m;
        Assert.Equal(new[] { "The field Price must be between 0 and 1000000." }, Validate(item));
    }

    [Theory]
    [InlineData(10, 12, 2)]
    [InlineData(5, 12, 3)]
    [InlineData(1, 12, 12)]
    [InlineData(10, 0, 0)]
    [InlineData(-1, 12, -12)]
    public void TotalPages_is_ceiling_of_count_over_size(int size, long count, int pages)
    {
        var vm = new PaginatedItemsViewModel<CatalogItem>(1, size, count, []);
        Assert.Equal(pages, vm.TotalPages);
        Assert.Equal(1, vm.ActualPage);
        Assert.Equal(size, vm.ItemsPerPage);
        Assert.Equal(count, vm.TotalItems);
    }

    [Fact]
    public void Page_size_zero_throws_like_legacy_Q5() =>
        Assert.Throws<DivideByZeroException>(() => new PaginatedItemsViewModel<CatalogItem>(0, 0, 12, []));

    [Fact]
    public void Default_picture_is_dummy_png() => Assert.Equal("dummy.png", new CatalogItem().PictureFileName);

    [Fact]
    public void Pagination_matches_legacy_rounding() => Assert.Equal(3, new PaginatedItemsViewModel<CatalogItem>(0, 5, 12, []).TotalPages);

    [Fact]
    public void Catalog_section_override_is_not_masked_by_root_defaults()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Catalog:UseMockData"] = "true" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Catalog:UseMockData"] = "false", ["Catalog:UseCustomizationData"] = "true" })
            .Build();
        var s = cfg.GetCatalogSettings();
        Assert.False(s.UseMockData);
        Assert.True(s.UseCustomizationData);
    }

    [Fact]
    public void UseMockData_defaults_true_and_is_overridable()
    {
        Assert.True(new ConfigurationBuilder().Build().GetCatalogSettings().UseMockData);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["UseMockData"] = "false" }).Build();
        Assert.False(cfg.GetCatalogSettings().UseMockData);
    }
}
