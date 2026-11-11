using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Catalog.WebForms.Tests.S6_PagesHost;

/// <summary>TimeProvider whose local clock only moves when a test says so.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2024, 3, 5, 14, 7, 9, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => now += by;
}

/// <summary>Minimal in-memory ISession that counts writes.</summary>
public sealed class RecordingSession : ISession
{
    private readonly Dictionary<string, byte[]> values = new();

    public int SetCalls { get; private set; }

    public int LoadCalls { get; private set; }

    public bool IsAvailable => true;

    public string Id { get; } = Guid.NewGuid().ToString();

    public IEnumerable<string> Keys => values.Keys;

    public void Clear() => values.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadCalls++;
        return Task.CompletedTask;
    }

    public void Remove(string key) => values.Remove(key);

    public void Set(string key, byte[] value)
    {
        SetCalls++;
        values[key] = value;
    }

    public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
}

public sealed class SessionFeature : Microsoft.AspNetCore.Http.Features.ISessionFeature
{
    public ISession Session { get; set; } = default!;
}

public static class S6Host
{
    public static WebApplicationFactory<Program> WithClock(this WebApplicationFactory<Program> factory, ManualTimeProvider clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    public static HttpClient NoRedirectClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static string Footer(string html)
    {
        var match = Regex.Match(html, "<span id=\"ctl00_SessionInfoLabel\">([^<]*)</span>");
        Assert.True(match.Success, "footer session label not rendered");
        return match.Groups[1].Value;
    }

    public static string RepoModernDir([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    public static string WwwRoot => Path.Combine(RepoModernDir(), "src", "eShop.Catalog.WebForms", "wwwroot");

    /// <summary>Body of a committed legacy baseline capture (parity/baseline/mock/legacy-webforms).</summary>
    public static string LegacyBaselineBody(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepoModernDir(), "parity", "baseline", "mock", "legacy-webforms", file));
        return text[(text.IndexOf("\n---\n", StringComparison.Ordinal) + 5)..].TrimEnd();
    }

    /// <summary>C# port of parity/capture_baseline.py normalise_html (minus origin rewriting).</summary>
    public static string NormaliseHtml(string text)
    {
        text = Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"(<script\b[^>]*>).*?(</script>)", "$1$2", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (var field in new[] { "__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION" })
        {
            text = Regex.Replace(text, "(name=\"" + field + "\"[^>]*?value=\")[^\"]*(\")", "$1{masked}$2");
            text = Regex.Replace(text, "(value=\")[^\"]*(\"[^>]*?name=\"" + field + "\")", "$1{masked}$2");
        }

        text = Regex.Replace(text, @"(<small[^>]*>(?:<span[^>]*>)?)\s*[^<>,]+,\s*\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}(?:\s*[AP]M)?\s*", "$1{masked}");
        text = Regex.Replace(text, @"\s+", " ");
        var lines = Regex.Replace(text, @"\s*(<)", "\n$1").Split('\n');
        return string.Join("\n", lines.Select(l => l.Trim()).Where(l => l.Length > 0));
    }

    /// <summary>
    /// The page-state hidden input is the one intended difference: legacy __VIEWSTATE vs the antiforgery token.
    /// </summary>
    public static string MaskPageState(string normalised) =>
        Regex.Replace(normalised, "<input[^>]*name=\"(__VIEWSTATE|__RequestVerificationToken)\"[^>]*/>", "{page-state}");

    public static async Task<string> GetOkHtml(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
