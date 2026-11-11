using System.Globalization;
using eShop.Catalog.Mvc.Hosting;
using Microsoft.AspNetCore.Http;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public class CatalogSessionInitializerTests
{
    [Fact]
    public void EnsureStarted_sets_machine_name_and_start_time_once()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 13, 15, 4, 5, TimeSpan.Zero));
        var initializer = new CatalogSessionInitializer(time, "CATALOG-HOST");
        var session = new DictionarySession();

        Assert.True(initializer.EnsureStarted(session));
        time.Advance(TimeSpan.FromHours(2));
        Assert.False(initializer.EnsureStarted(session));

        Assert.Equal(2, session.SetCount);
        Assert.Equal("CATALOG-HOST", session.GetString(CatalogSessionInitializer.MachineNameKey));
        Assert.Equal("11/13/2026 3:04:05 PM", session.GetString(CatalogSessionInitializer.SessionStartTimeKey));
    }

    [Fact]
    public void GetFooterText_starts_an_empty_session_and_is_stable_afterwards()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 2, 9, 8, 7, TimeSpan.Zero));
        var initializer = new CatalogSessionInitializer(time, "web01");
        var session = new DictionarySession();

        var first = initializer.GetFooterText(session);
        time.Advance(TimeSpan.FromMinutes(5));
        var second = initializer.GetFooterText(session);

        Assert.Equal("web01, 1/2/2026 9:08:07 AM", first);
        Assert.Equal(first, second);
        Assert.Equal("web01, 1/2/2026 9:13:07 AM", initializer.GetFooterText(new DictionarySession()));
    }

    [Theory]
    [InlineData(2026, 11, 13, 15, 4, 5, "11/13/2026 3:04:05 PM")]
    [InlineData(2026, 1, 2, 0, 0, 0, "1/2/2026 12:00:00 AM")]
    [InlineData(2026, 12, 31, 12, 30, 59, "12/31/2026 12:30:59 PM")]
    public void FormatSessionStartTime_uses_legacy_en_US_DateTime_ToString(int y, int mo, int d, int h, int mi, int s, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var formatted = CatalogSessionInitializer.FormatSessionStartTime(new DateTime(y, mo, d, h, mi, s));

            Assert.Equal(expected, formatted);
            Assert.DoesNotContain('\u202F', formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
