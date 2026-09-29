namespace eShopCatalog.Contracts
{
    /// <summary>
    /// HTTP routes for each operation of the legacy WCF <c>ICatalogService</c> contract.
    /// Shared by the server and the client so both sides stay in sync.
    /// </summary>
    public static class CatalogRoutes
    {
        public const string Base = "api/catalog";

        public const string Items = Base + "/items";
        public const string ItemById = Items + "/{id:int}";
        public const string ItemStock = Items + "/{catalogItemId:int}/stock";
        public const string Brands = Base + "/brands";
        public const string Types = Base + "/types";
        public const string Stock = Base + "/stock";
        public const string Discount = Base + "/discount";

        /// <summary>Wire format for date-only query parameters (<c>date</c>, <c>day</c>).</summary>
        public const string DateFormat = "yyyy-MM-dd";
    }
}
