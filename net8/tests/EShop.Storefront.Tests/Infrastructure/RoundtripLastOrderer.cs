using Xunit;
using Xunit.Abstractions;

[assembly: TestCollectionOrderer("EShop.Storefront.Tests.Infrastructure.RoundtripLastOrderer", "EShop.Storefront.Tests")]

namespace EShop.Storefront.Tests.Infrastructure;

/// <summary>
/// Collections run sequentially (xunit.runner.json); mutating round-trip classes run last so a live server
/// (ESHOP_TEST_*_URL) sees the read-only tests against the seeded catalog first, like the Python suite's test_zz_* ordering.
/// </summary>
public sealed class RoundtripLastOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections
            .OrderBy(c => c.DisplayName.Contains("Roundtrip", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal);
}
