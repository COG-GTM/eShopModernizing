using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eShopWCFService.Models;

public class DiscountItem
{
    public double Size { get; set; }

    [Column(TypeName = "date")]
    public DateTime Start { get; set; }

    [Column(TypeName = "date")]
    public DateTime End { get; set; }

    [Key]
    public int Id { get; set; }
}
