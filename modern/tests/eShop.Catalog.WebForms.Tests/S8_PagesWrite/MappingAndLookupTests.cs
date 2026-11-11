using eShop.Catalog.Data.Models;
using eShop.Catalog.WebForms.Pages.Catalog;

namespace eShop.Catalog.WebForms.Tests.S8_PagesWrite;

public class MappingAndLookupTests
{
    private static readonly int[] BrandIds = [1, 2, 3, 4, 5];

    [Fact]
    public void ApplyTo_patches_posted_fields_and_preserves_PictureFileName_and_OnReorder()
    {
        var item = new CatalogItem
        {
            Id = 7,
            Name = "Old",
            Description = "Old description",
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            Price = 1m,
            PictureFileName = "7.png",
            OnReorder = true,
            AvailableStock = 1,
            RestockThreshold = 1,
            MaxStockThreshold = 1,
        };
        var input = new EditCatalogItemInput { Name = "New", Description = "New description", Price = "1,234.5", Stock = "10", Restock = "2", Maxstock = "20" };

        input.ApplyTo(item, brandId: 3, typeId: 4);

        Assert.Equal(7, item.Id);
        Assert.Equal("New", item.Name);
        Assert.Equal("New description", item.Description);
        Assert.Equal(3, item.CatalogBrandId);
        Assert.Equal(4, item.CatalogTypeId);
        Assert.Equal(1234.5m, item.Price);
        Assert.Equal(10, item.AvailableStock);
        Assert.Equal(2, item.RestockThreshold);
        Assert.Equal(20, item.MaxStockThreshold);
        Assert.Equal("7.png", item.PictureFileName);
        Assert.True(item.OnReorder);
    }

    [Fact]
    public void ToNewCatalogItem_uses_default_picture_and_not_on_reorder()
    {
        var item = ValidationBoundaryTests.ValidCreateInput().ToNewCatalogItem(brandId: 2, typeId: 3);

        Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
        Assert.False(item.OnReorder);
        Assert.Equal(2, item.CatalogBrandId);
        Assert.Equal(3, item.CatalogTypeId);
    }

    [Fact]
    public void Input_models_expose_no_PictureFileName_OnReorder_or_Id_to_binding()
    {
        foreach (var type in new[] { typeof(CreateCatalogItemInput), typeof(EditCatalogItemInput) })
        {
            var settable = type.GetProperties().Where(p => p.CanWrite).Select(p => p.Name).ToList();
            Assert.DoesNotContain(nameof(CatalogItem.PictureFileName), settable);
            Assert.DoesNotContain(nameof(CatalogItem.OnReorder), settable);
            Assert.DoesNotContain(nameof(CatalogItem.Id), settable);
        }

        Assert.DoesNotContain(typeof(EditModel).GetProperty(nameof(EditModel.PictureFileName))!.GetCustomAttributes(true), a => a is Microsoft.AspNetCore.Mvc.BindPropertyAttribute);
        Assert.False(typeof(EditModel).GetProperty(nameof(EditModel.PictureFileName))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void ApplyTo_unparseable_number_changes_nothing()
    {
        var item = new CatalogItem { Name = "Keep", Price = 5m, PictureFileName = "5.png" };
        var input = new EditCatalogItemInput { Name = "New", Price = "", Stock = "1", Restock = "1", Maxstock = "1" };

        Assert.Throws<FormatException>(() => input.ApplyTo(item, 1, 1));
        Assert.Equal("Keep", item.Name);
        Assert.Equal(5m, item.Price);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    public void Rendered_brand_or_type_is_accepted(string posted, int expected)
    {
        Assert.True(CatalogLookups.TryResolve(posted, BrandIds, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("01")]
    [InlineData("abc")]
    public void Unknown_brand_or_type_is_rejected(string? posted)
    {
        Assert.False(CatalogLookups.TryResolve(posted, BrandIds, out _));
        var ex = Assert.Throws<ArgumentException>(() => CatalogLookups.Resolve(posted, BrandIds));
        Assert.Equal(CatalogLookups.InvalidPostbackMessage, ex.Message);
    }
}
