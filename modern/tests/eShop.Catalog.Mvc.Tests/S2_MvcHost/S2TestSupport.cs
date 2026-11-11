using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using eShop.Catalog.Mvc.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void Advance(TimeSpan by) => utcNow = utcNow.Add(by);
}

public sealed class DictionarySession : ISession
{
    private readonly Dictionary<string, byte[]> values = new();

    public bool IsAvailable => true;

    public string Id { get; } = Guid.NewGuid().ToString();

    public IEnumerable<string> Keys => values.Keys;

    public int SetCount { get; private set; }

    public void Clear() => values.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => values.Remove(key);

    public void Set(string key, byte[] value)
    {
        SetCount++;
        values[key] = value;
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => values.TryGetValue(key, out value);
}

/// <summary>Test-only MVC controller that renders through the S2 layout (S3/S4 own the real catalog views).</summary>
[Route("__s2-probe")]
public sealed class S2ProbeController : Controller
{
    [HttpGet("layout")]
    public IActionResult Layout()
    {
        ViewData["Title"] = "Index";
        return View(MvcUiExtensions.ErrorViewPath);
    }

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("boom");

    [HttpGet("not-found")]
    public IActionResult ActionNotFound() => NotFound();
}

/// <summary>Production host with a controllable clock and the probe controller registered.</summary>
public sealed class S2HostFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Start = new(2026, 11, 13, 15, 4, 5, TimeSpan.Zero);

    public ManualTimeProvider Time { get; } = new(Start);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddControllersWithViews().AddApplicationPart(typeof(S2ProbeController).Assembly);
        });
    }
}

/// <summary>Same HTML normalisation as modern/parity/capture_baseline.py, so modern output compares to the legacy baseline.</summary>
public static class LegacyBaseline
{
    public static string ModernRoot { get; } = FindModernRoot();

    public static string LegacyMvcRoot => Path.GetFullPath(Path.Combine(ModernRoot, "..", "eShopLegacyMVCSolution", "src", "eShopLegacyMVC"));

    public static string ReadNormalisedBody(string file)
    {
        var text = File.ReadAllText(Path.Combine(ModernRoot, "parity", "baseline", "mock", "legacy-mvc", file));
        return text[(text.IndexOf("\n---\n", StringComparison.Ordinal) + 5)..].TrimEnd('\n');
    }

    public static string NormaliseHtml(string html)
    {
        var text = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"(<script\b[^>]*>).*?(</script>)", "$1$2", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(<small[^>]*>(?:<span[^>]*>)?)\s*[^<>,]+,\s*\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}(?:\s*[AP]M)?\s*", "$1{masked}");
        text = Regex.Replace(text, @"\s+", " ");
        var lines = Regex.Replace(text, @"\s*(<)", "\n$1").Split('\n');
        return string.Join("\n", lines.Select(l => l.Trim()).Where(l => l.Length > 0));
    }

    /// <summary>The normalised lines from the first line starting with <paramref name="from"/> through the first later line starting with <paramref name="to"/>.</summary>
    public static string Slice(string normalised, string from, string to)
    {
        var lines = normalised.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith(from, StringComparison.Ordinal));
        Assert.True(start >= 0, $"'{from}' not found in:\n{normalised}");
        var end = Array.FindIndex(lines, start, l => l.StartsWith(to, StringComparison.Ordinal));
        Assert.True(end >= 0, $"'{to}' not found after '{from}' in:\n{normalised}");
        return string.Join("\n", lines[start..(end + 1)]);
    }

    public static string? FooterText(string html)
    {
        var match = Regex.Match(html, @"<small>(.*?)</small>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string FindModernRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "eShop.Catalog.Modern.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("modern/ (eShop.Catalog.Modern.sln) not found above " + AppContext.BaseDirectory);
    }
}
