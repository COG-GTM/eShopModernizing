using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eShopWCFService.Models;

[Table("CatalogItemsStock")]
public partial class CatalogItemsStock
{
    [Column(TypeName = "date")]
    public DateTime Date { get; set; }

    public int CatalogItemId { get; set; }

    public int AvailableStock { get; set; }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int StockId { get; set; }
}
