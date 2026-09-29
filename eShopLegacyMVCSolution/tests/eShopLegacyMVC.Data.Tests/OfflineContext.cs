using eShopLegacyMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopLegacyMVC.Data.Tests
{
    /// <summary>
    /// A SQL Server-configured context that never opens a connection: enough for model inspection, script generation
    /// and the validate-on-save checks, which all run before EF Core touches the database.
    /// </summary>
    internal static class OfflineContext
    {
        public const string UnreachableConnectionString = "Server=unreachable.invalid;Database=Catalog;Connect Timeout=1;";

        public static CatalogDBContext Create()
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseSqlServer(UnreachableConnectionString)
                .Options;
            return new CatalogDBContext(options);
        }
    }
}
