using System.ComponentModel.DataAnnotations;

namespace eShopCoreAPI.Models;

public class CatalogItem
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage = "The field Price must be a positive number with maximum two decimals.")]
    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Display(Name = "Picture name")]
    public string PictureFileName { get; set; } = string.Empty;

    public string PictureUri { get; set; } = string.Empty;

    [Display(Name = "Type")]
    public int CatalogTypeId { get; set; }

    [Display(Name = "Type")]
    public CatalogType? CatalogType { get; set; }

    [Display(Name = "Brand")]
    public int CatalogBrandId { get; set; }

    [Display(Name = "Brand")]
    public CatalogBrand? CatalogBrand { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
    [Display(Name = "Stock")]
    public int AvailableStock { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Restock must be between 0 and 10 million.")]
    [Display(Name = "Restock")]
    public int RestockThreshold { get; set; }

    [Range(0, 10000000, ErrorMessage = "The field Max stock must be between 0 and 10 million.")]
    [Display(Name = "Max stock")]
    public int MaxStockThreshold { get; set; }

    public bool OnReorder { get; set; }

    public string TempImageName { get; set; } = string.Empty;
}
