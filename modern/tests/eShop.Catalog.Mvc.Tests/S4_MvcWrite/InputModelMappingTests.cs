using eShop.Catalog.Data.Models;
using eShop.Catalog.Mvc.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

public class InputModelMappingTests
{
    private static CatalogItemInputModel Posted() => new()
    {
        Id = 99,
        Name = "Edited",
        Description = "Edited description",
        Price = 21.00m,
        CatalogBrandId = 3,
        CatalogTypeId = 4,
        AvailableStock = 5,
        RestockThreshold = 6,
        MaxStockThreshold = 7,
        PictureFileName = "../../etc/passwd",
        PictureUri = "http://evil.example/x.png",
    };

    [Fact]
    public void ApplyTo_copies_editable_fields_and_preserves_PictureFileName_OnReorder_and_Id()
    {
        var existing = new CatalogItem
        {
            Id = 1, Name = "Old", Price = 19.50m, PictureFileName = "1.png", OnReorder = true,
            CatalogBrandId = 2, CatalogTypeId = 2, AvailableStock = 100, RestockThreshold = 10, MaxStockThreshold = 200,
        };

        Posted().ApplyTo(existing);

        Assert.Equal(1, existing.Id);
        Assert.Equal("1.png", existing.PictureFileName);
        Assert.True(existing.OnReorder);
        Assert.Null(existing.PictureUri);
        Assert.Equal("Edited", existing.Name);
        Assert.Equal("Edited description", existing.Description);
        Assert.Equal(21.00m, existing.Price);
        Assert.Equal(3, existing.CatalogBrandId);
        Assert.Equal(4, existing.CatalogTypeId);
        Assert.Equal(5, existing.AvailableStock);
        Assert.Equal(6, existing.RestockThreshold);
        Assert.Equal(7, existing.MaxStockThreshold);
    }

    [Fact]
    public void ToNewEntity_uses_default_picture_and_OnReorder_false_regardless_of_input()
    {
        var created = Posted().ToNewEntity();

        Assert.Equal(CatalogItem.DefaultPictureName, created.PictureFileName);
        Assert.False(created.OnReorder);
        Assert.Equal(0, created.Id);
        Assert.Equal("Edited", created.Name);
        Assert.Equal(21.00m, created.Price);
    }

    [Fact]
    public void FromEntity_round_trips_display_fields()
    {
        var entity = new CatalogItem
        {
            Id = 1, Name = "Mug", Description = "d", Price = 19.5m, PictureFileName = "1.png", PictureUri = "/items/1/pic",
            CatalogBrandId = 2, CatalogTypeId = 1, AvailableStock = 100, RestockThreshold = 10, MaxStockThreshold = 200,
        };

        var model = CatalogItemInputModel.FromEntity(entity);

        Assert.Equal(1, model.Id);
        Assert.Equal("1.png", model.PictureFileName);
        Assert.Equal("/items/1/pic", model.PictureUri);
        Assert.Equal(19.5m, model.Price);
        Assert.Equal(200, model.MaxStockThreshold);
    }

    [Theory]
    [InlineData(nameof(CatalogItemInputModel.PictureFileName))]
    [InlineData(nameof(CatalogItemInputModel.PictureUri))]
    public void Picture_fields_are_never_model_bound(string property)
    {
        var attribute = typeof(CatalogItemInputModel).GetProperty(property)!
            .GetCustomAttributes(typeof(BindNeverAttribute), inherit: true);

        Assert.Single(attribute);
        Assert.Null(typeof(CatalogItemInputModel).GetProperty("OnReorder"));
    }
}
