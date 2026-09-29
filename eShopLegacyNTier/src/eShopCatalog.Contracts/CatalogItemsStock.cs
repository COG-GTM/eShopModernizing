using System;

namespace eShopCatalog.Contracts
{
    public class CatalogItemsStock
    {
        public DateTime Date { get; set; }
        public int CatalogItemId { get; set; }
        public int AvailableStock { get; set; }
        public int StockId { get; set; }
    }
}
