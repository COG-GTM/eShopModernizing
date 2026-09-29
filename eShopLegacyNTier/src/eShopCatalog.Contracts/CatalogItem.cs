namespace eShopCatalog.Contracts
{
    public class CatalogItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Picturefilename { get; set; }
        public int CatalogBrandId { get; set; }
        public int CatalogTypeId { get; set; }
        public CatalogType CatalogType { get; set; }
        public CatalogBrand CatalogBrand { get; set; }
    }
}
