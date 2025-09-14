using Microsoft.EntityFrameworkCore;

namespace eShopModernizedCore.Models;

public class CatalogItemHiLoGenerator
{
    private const int LoSize = 9;
    private const string SequenceName = "catalog_hilo";
    
    private int _currentHi = -1;
    private int _remainingLoIds = 0;
    private readonly object _sequenceLock = new();

    public async Task<int> GetNextSequenceValueAsync(CatalogDbContext context)
    {
        lock (_sequenceLock)
        {
            if (_remainingLoIds == 0)
            {
                var rawQuery = context.Database.SqlQueryRaw<long>($"SELECT NEXT VALUE FOR {SequenceName}");
                _currentHi = (int)rawQuery.AsEnumerable().First();
                _remainingLoIds = LoSize;
            }

            _remainingLoIds--;
            return (_currentHi * (LoSize + 1)) + (LoSize - _remainingLoIds);
        }
    }

    public int GetNextSequenceValue(CatalogDbContext context)
    {
        return GetNextSequenceValueAsync(context).GetAwaiter().GetResult();
    }
}
