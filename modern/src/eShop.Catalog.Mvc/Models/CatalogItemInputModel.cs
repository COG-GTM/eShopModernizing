using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Data.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace eShop.Catalog.Mvc.Models;

/// <summary>
/// Create/Edit form model. Replaces the legacy <c>[Bind(Include = ...)]</c> on <see cref="CatalogItem"/>: only the
/// editable fields bind from the request, so <c>PictureFileName</c>, <c>PictureUri</c> and <c>OnReorder</c> can't be
/// over-posted. Display names and validation attributes mirror <see cref="CatalogItem"/> so labels, <c>data-val-*</c>
/// attributes and server messages match legacy. Value types are nullable + [Required] so an empty field yields the
/// legacy "The X field is required." message instead of ASP.NET Core's "The value '' is invalid.".
/// </summary>
public class CatalogItemInputModel
{
    public int Id { get; set; }

    [Required]
    [ColumnMaxLength(50)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Required]
    [RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage = "The field Price must be a positive number with maximum two decimals.")]
    [SafeRange(0, 1000000)]
    [DataType(DataType.Currency)]
    public decimal? Price { get; set; }

    [BindNever]
    [Display(Name = "Picture name")]
    public string? PictureFileName { get; set; }

    [BindNever]
    public string? PictureUri { get; set; }

    [Required]
    [Display(Name = "Type")]
    public int? CatalogTypeId { get; set; }

    [Required]
    [Display(Name = "Brand")]
    public int? CatalogBrandId { get; set; }

    [Required]
    [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
    [Display(Name = "Stock")]
    public int? AvailableStock { get; set; }

    [Required]
    [Range(0, 10000000, ErrorMessage = "The field Restock must be between 0 and 10 million.")]
    [Display(Name = "Restock")]
    public int? RestockThreshold { get; set; }

    [Required]
    [Range(0, 10000000, ErrorMessage = "The field Max stock must be between 0 and 10 million.")]
    [Display(Name = "Max stock")]
    public int? MaxStockThreshold { get; set; }

    public static CatalogItemInputModel FromEntity(CatalogItem item)
    {
        return new CatalogItemInputModel
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            PictureFileName = item.PictureFileName,
            PictureUri = item.PictureUri,
            CatalogTypeId = item.CatalogTypeId,
            CatalogBrandId = item.CatalogBrandId,
            AvailableStock = item.AvailableStock,
            RestockThreshold = item.RestockThreshold,
            MaxStockThreshold = item.MaxStockThreshold,
        };
    }

    /// <summary>A new item: <c>PictureFileName</c> keeps the entity default (legacy "dummy.png"), <c>OnReorder</c> false.</summary>
    public CatalogItem ToNewEntity()
    {
        var item = new CatalogItem();
        ApplyTo(item);
        return item;
    }

    /// <summary>Copies the editable fields only; <c>Id</c>, <c>PictureFileName</c>, <c>PictureUri</c> and <c>OnReorder</c> are preserved.</summary>
    public void ApplyTo(CatalogItem item)
    {
        item.Name = Name ?? string.Empty;
        item.Description = Description;
        item.Price = Price.GetValueOrDefault();
        item.CatalogTypeId = CatalogTypeId.GetValueOrDefault();
        item.CatalogBrandId = CatalogBrandId.GetValueOrDefault();
        item.AvailableStock = AvailableStock.GetValueOrDefault();
        item.RestockThreshold = RestockThreshold.GetValueOrDefault();
        item.MaxStockThreshold = MaxStockThreshold.GetValueOrDefault();
    }
}
