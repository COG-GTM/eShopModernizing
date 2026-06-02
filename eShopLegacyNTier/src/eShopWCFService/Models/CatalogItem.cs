using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eShopWCFService.Models;

public partial class CatalogItem
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "money")]
    public decimal Price { get; set; }

    public string Picturefilename { get; set; } = string.Empty;

    public int CatalogBrandId { get; set; }

    public int CatalogTypeId { get; set; }

    public CatalogType? CatalogType { get; set; }

    public CatalogBrand? CatalogBrand { get; set; }
}
