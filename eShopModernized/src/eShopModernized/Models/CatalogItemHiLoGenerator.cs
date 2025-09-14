using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Models
{
    public class CatalogItemHiLoGenerator
    {
        private const int HiLoIncrement = 10;
        private int sequenceId = -1;
        private int remainningLoIds = 0;
        private readonly object sequenceLock = new object();

        public async Task<int> GetNextSequenceValueAsync(CatalogDBContext db)
        {
            return await Task.Run(() => GetNextSequenceValue(db));
        }

        public int GetNextSequenceValue(CatalogDBContext db)
        {
            lock (sequenceLock)
            {
                if (remainningLoIds == 0)
                {
                    var connection = db.Database.GetDbConnection();
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT NEXT VALUE FOR catalog_hilo;";
                    var result = command.ExecuteScalar();
                    sequenceId = Convert.ToInt32(result);
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
    }
}
