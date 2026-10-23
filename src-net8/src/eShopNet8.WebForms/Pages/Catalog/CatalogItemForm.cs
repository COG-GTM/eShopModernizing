using eShop.Catalog.Core.Models;

namespace eShopNet8.WebForms.Pages.Catalog
{
    // Text values of the WebForms TextBox/DropDownList controls, rendered verbatim (TextBox.Text is a string).
    public sealed class CatalogItemForm
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string BrandId { get; set; }
        public string TypeId { get; set; }
        public string Price { get; set; }
        public string PictureFileName { get; set; }
        public string Stock { get; set; }
        public string Restock { get; set; }
        public string Maxstock { get; set; }

        public static CatalogItemForm From(CatalogItem product) => new()
        {
            Name = product.Name,
            Description = product.Description,
            BrandId = Convert.ToString(product.CatalogBrandId),
            TypeId = Convert.ToString(product.CatalogTypeId),
            Price = Convert.ToString(product.Price),
            PictureFileName = product.PictureFileName,
            Stock = Convert.ToString(product.AvailableStock),
            Restock = Convert.ToString(product.RestockThreshold),
            Maxstock = Convert.ToString(product.MaxStockThreshold),
        };
    }
}
