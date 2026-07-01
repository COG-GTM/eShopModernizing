using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Infrastructure
{
    public static class CatalogDBInitializer
    {
        private static readonly string[] HiLoSequences =
        {
            "catalog_hilo",
            "catalog_brand_hilo",
            "catalog_type_hilo"
        };

        public static async Task SeedAsync(CatalogDBContext context, ILogger logger)
        {
            var isSqlServer = context.Database.IsSqlServer();

            if (isSqlServer)
            {
                foreach (var sequence in HiLoSequences)
                {
                    await CreateSequenceIfNotExistsAsync(context, sequence);
                }
            }

            if (await context.CatalogItems.AnyAsync())
            {
                logger.LogInformation("Catalog database already contains data - skipping seed");
                return;
            }

            logger.LogInformation("Seeding catalog database with preconfigured data");

            context.CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
            context.CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
            await context.SaveChangesAsync();

            context.CatalogItems.AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
            await context.SaveChangesAsync();

            logger.LogInformation("Catalog database seeded successfully");
        }

        private static async Task CreateSequenceIfNotExistsAsync(CatalogDBContext context, string sequenceName)
        {
            var sql = $@"
IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = '{sequenceName}')
BEGIN
    EXEC('CREATE SEQUENCE [dbo].[{sequenceName}]
        AS [bigint]
        START WITH 100
        INCREMENT BY 10
        MINVALUE -9223372036854775808
        MAXVALUE 9223372036854775807
        CACHE');
END";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
