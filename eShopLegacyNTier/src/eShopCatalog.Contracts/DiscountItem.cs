using System;

namespace eShopCatalog.Contracts
{
    public class DiscountItem
    {
        public double Size { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Id { get; set; }
    }
}
