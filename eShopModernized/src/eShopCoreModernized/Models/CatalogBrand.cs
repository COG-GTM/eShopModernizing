using System.ComponentModel.DataAnnotations;

namespace eShopCoreModernized.Models
{
    public class CatalogBrand
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;
    }
}
