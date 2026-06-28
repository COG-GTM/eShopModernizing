using System.ComponentModel.DataAnnotations;

namespace Vans.Catalog.Api.Models;

public class CatalogItem
{
    public const string DefaultPictureName = "dummy.png";

    public CatalogItem()
    {
        PictureFileName = DefaultPictureName;
    }

    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // decimal(18,2)
    [RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage = "The field Price must be a positive number with maximum two decimals.")]
    [Range(0, 1000000)]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    public string PictureFileName { get; set; }

    public string? PictureUri { get; set; }

    public int CatalogTypeId { get; set; }

    public CatalogType? CatalogType { get; set; }

    public int CatalogBrandId { get; set; }

    public CatalogBrand? CatalogBrand { get; set; }

    // Quantity in stock
    [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
    public int AvailableStock { get; set; }

    // Available stock at which we should reorder
    [Range(0, 10000000, ErrorMessage = "The field Restock must be between 0 and 10 million.")]
    public int RestockThreshold { get; set; }

    // Maximum number of units that can be in-stock at any time (due to physical/logistical constraints in warehouses)
    [Range(0, 10000000, ErrorMessage = "The field Max stock must be between 0 and 10 million.")]
    public int MaxStockThreshold { get; set; }

    /// <summary>
    /// True if item is on reorder
    /// </summary>
    public bool OnReorder { get; set; }
}
