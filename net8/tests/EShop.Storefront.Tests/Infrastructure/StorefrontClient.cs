using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace EShop.Storefront.Tests.Infrastructure;

/// <summary>Browser-like client: one cookie jar per app, redirects not followed (same as the Python characterization suite).</summary>
public sealed class StorefrontClient(HttpClient http)
{
    public Uri BaseAddress => http.BaseAddress!;

    public Task<Page> GetAsync(string path, string? accept = null) => SendAsync(HttpMethod.Get, path, accept: accept);

    public async Task<Page> SendAsync(HttpMethod method, string path, HttpContent? content = null, string? accept = null)
    {
        var url = new Uri(BaseAddress, path.TrimStart('/'));
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        using var response = await http.SendAsync(request);
        return new Page(url, (int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? string.Empty,
            response.Headers.Location?.OriginalString, await response.Content.ReadAsByteArrayAsync());
    }

    public Task<Page> PostFormAsync(Uri url, IEnumerable<KeyValuePair<string, string>> fields) =>
        SendAsync(HttpMethod.Post, url.IsAbsoluteUri ? url.PathAndQuery : url.OriginalString, new FormUrlEncodedContent(fields));
}

public sealed class Page(Uri url, int status, string contentType, string? location, byte[] body)
{
    private IDocument? _document;

    public Uri Url { get; } = url;
    public int Status { get; } = status;
    public string ContentType { get; } = contentType;
    public string? Location { get; } = location;
    public byte[] Body { get; } = body;
    public string Text => System.Text.Encoding.UTF8.GetString(Body);
    public IDocument Html => _document ??= new HtmlParser().ParseDocument(Text);
}
