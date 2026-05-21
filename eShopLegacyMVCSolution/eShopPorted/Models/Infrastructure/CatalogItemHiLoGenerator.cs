using Microsoft.EntityFrameworkCore;

namespace eShopPorted.Models.Infrastructure
{
    public class CatalogItemHiLoGenerator
    {
        private const int HiLoIncrement = 10;
        private int sequenceId = -1;
        private int remainingLoIds = 0;
        private readonly object sequenceLock = new object();

        public int GetNextSequenceValue(CatalogDBContext db)
        {
            lock (sequenceLock)
            {
                if (remainingLoIds == 0)
                {
                    var result = db.Database.SqlQueryRaw<long>("SELECT NEXT VALUE FOR catalog_hilo").ToList();
                    sequenceId = (int)result.Single();
                    remainingLoIds = HiLoIncrement - 1;
                    return sequenceId;
                }
                else
                {
                    remainingLoIds--;
                    return ++sequenceId;
                }
            }
        }
    }
}
