using Microsoft.EntityFrameworkCore;

namespace EShop.Catalog.Data;

/// <summary>
/// Legacy <c>CatalogItemHiLoGenerator</c> (singleton): one <c>NEXT VALUE FOR catalog_hilo</c> reserves a block of
/// <see cref="HiLoIncrement"/> ids (the sequence increments by 10); the next 9 ids are handed out in memory.
/// </summary>
public class CatalogItemHiLoGenerator
{
    public const int HiLoIncrement = 10;
    public const string SequenceName = "catalog_hilo";

    private readonly object sequenceLock = new();
    private int sequenceId = -1;
    private int remainingLoIds;

    public int GetNextSequenceValue(CatalogDbContext db)
    {
        lock (sequenceLock)
        {
            if (remainingLoIds == 0)
            {
                sequenceId = (int)NextSequenceValue(db, SequenceName);
                remainingLoIds = HiLoIncrement - 1;
                return sequenceId;
            }

            remainingLoIds--;
            return ++sequenceId;
        }
    }

    internal static long NextSequenceValue(CatalogDbContext db, string sequenceName)
    {
        var sql = sequenceName switch
        {
            SequenceName => "SELECT NEXT VALUE FOR [dbo].[catalog_hilo] AS [Value]",
            "catalog_brand_hilo" => "SELECT NEXT VALUE FOR [dbo].[catalog_brand_hilo] AS [Value]",
            "catalog_type_hilo" => "SELECT NEXT VALUE FOR [dbo].[catalog_type_hilo] AS [Value]",
            _ => throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unknown catalog sequence"),
        };
        return db.Database.SqlQueryRaw<long>(sql).AsEnumerable().Single();
    }
}
