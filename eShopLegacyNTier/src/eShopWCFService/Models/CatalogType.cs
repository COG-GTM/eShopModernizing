using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eShopWCFService.Models;

public partial class CatalogType
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [StringLength(50)]
    public string Type { get; set; } = string.Empty;
}
