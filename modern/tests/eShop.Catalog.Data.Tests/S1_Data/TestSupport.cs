using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Data.Tests.S1_Data;

internal static class TestSupport
{
    public static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    public static CatalogDbContext InMemoryContext(string? name = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptors)
            .Options;
        return new CatalogDbContext(options);
    }

    public static CatalogSeeder Seeder(bool useCustomizationData = false, string? setupPath = null, string? picturesPath = null) =>
        new(
            new CatalogDataOptions { UseMockData = false, UseCustomizationData = useCustomizationData },
            Options.Create(new CatalogSeedingOptions { SetupPath = setupPath ?? NonExistentDirectory(), PicturesPath = picturesPath ?? NonExistentDirectory() }));

    public static string NonExistentDirectory() => Path.Combine(Path.GetTempPath(), "eshop-s1-missing-" + Guid.NewGuid().ToString("N"));

    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "eshop-s1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

/// <summary>Records the entity types added in each SaveChanges call, in order.</summary>
internal sealed class SaveOrderInterceptor : SaveChangesInterceptor
{
    public List<string[]> Batches { get; } = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Batches.Add(eventData.Context!.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.GetType().Name)
            .Distinct()
            .ToArray());
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
