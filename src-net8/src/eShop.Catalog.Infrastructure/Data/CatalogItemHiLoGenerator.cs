using Microsoft.EntityFrameworkCore;

namespace eShop.Catalog.Infrastructure.Data
{
    // Port of eShopLegacyMVC.Models.CatalogItemHiLoGenerator: a singleton that reserves blocks of 10 ids
    // from catalog_hilo (INCREMENT BY 10) and hands them out one by one.
    public class CatalogItemHiLoGenerator
    {
        private const int HiLoIncrement = 10;
        private int sequenceId = -1;
        private int remainningLoIds = 0;
        private readonly object sequenceLock = new object();

        public int GetNextSequenceValue(CatalogDBContext db)
        {
            lock (sequenceLock)
            {
                if (remainningLoIds == 0)
                {
                    sequenceId = (int)NextSequenceValue(db, CatalogDBContext.CatalogItemHiLoSequenceName);
                    remainningLoIds = HiLoIncrement - 1;
                    return sequenceId;
                }
                else
                {
                    remainningLoIds--;
                    return ++sequenceId;
                }
            }
        }

        // NEXT VALUE FOR is not allowed inside a derived table, so the raw query must not be composed
        // (AsEnumerable keeps Single() on the client).
        internal static long NextSequenceValue(CatalogDBContext db, string sequenceName)
        {
            return db.Database
                .SqlQueryRaw<long>(NextValueQueries[sequenceName])
                .AsEnumerable()
                .Single();
        }

        private static readonly Dictionary<string, string> NextValueQueries = new()
        {
            [CatalogDBContext.CatalogItemHiLoSequenceName] = "SELECT NEXT VALUE FOR [dbo].[catalog_hilo] AS [Value]",
            [CatalogDBContext.CatalogBrandHiLoSequenceName] = "SELECT NEXT VALUE FOR [dbo].[catalog_brand_hilo] AS [Value]",
            [CatalogDBContext.CatalogTypeHiLoSequenceName] = "SELECT NEXT VALUE FOR [dbo].[catalog_type_hilo] AS [Value]",
        };
    }
}
