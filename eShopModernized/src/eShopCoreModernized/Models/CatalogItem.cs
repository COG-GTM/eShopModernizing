using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace eShopCoreModernized.Models
{
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

        [RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage = "The field Price must be a positive number with maximum two decimals.")]
        [Range(0, 1000000)]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Picture name")]
        public string PictureFileName { get; set; } = DefaultPictureName;

        public string? PictureUri { get; set; }
        public string? TempImageName { get; set; }

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
    }
}
