using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace eShopLegacyMVC.Models
{
    public class CatalogItemHiLoGenerator
    {
        private const int HiLoIncrement = 10;
        private int sequenceId = -1;
        private int remainningLoIds = 0;
        private object sequenceLock = new object();

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

        // SQL Server rejects NEXT VALUE FOR inside a derived table, so the raw query must not be composed on:
        // AsEnumerable() makes Single() run client-side instead of wrapping the SQL in a subquery.
        internal static long NextSequenceValue(CatalogDBContext db, string sequenceName)
        {
            if (sequenceName != CatalogDBContext.CatalogItemHiLoSequenceName
                && sequenceName != CatalogDBContext.CatalogBrandHiLoSequenceName
                && sequenceName != CatalogDBContext.CatalogTypeHiLoSequenceName)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unknown catalog sequence.");
            }

            var sql = "SELECT NEXT VALUE FOR [" + sequenceName + "] AS [Value]";
            return db.Database
                .SqlQueryRaw<long>(sql)
                .AsEnumerable()
                .Single();
        }
    }
}
