using Microsoft.Net.Http.Headers;

namespace EShop.Storefront.Mvc.Api;

internal enum LegacyFormatter
{
    Json,
    Xml,
}

internal readonly record struct NegotiationResult(LegacyFormatter Formatter, string MediaType);

/// <summary>
/// Web API 2 <c>DefaultContentNegotiator</c> over the default formatter list
/// (JsonMediaTypeFormatter: application/json, text/json; XmlMediaTypeFormatter: application/xml, text/xml).
/// Accept-header matches win by quality, then literal &gt; subtype range &gt; */*, ties keep formatter order;
/// otherwise the request Content-Type, otherwise the first formatter that can write the type (JSON).
/// </summary>
internal static class LegacyContentNegotiator
{
    private static readonly (LegacyFormatter Formatter, string[] MediaTypes)[] Formatters =
    {
        (LegacyFormatter.Json, new[] { "application/json", "text/json" }),
        (LegacyFormatter.Xml, new[] { "application/xml", "text/xml" }),
    };

    private enum Ranking
    {
        AllMediaRange,
        SubtypeMediaRange,
        Literal,
    }

    private readonly record struct Match(LegacyFormatter Formatter, string MediaType, double Quality, Ranking Ranking);

    public static NegotiationResult Negotiate(HttpRequest request)
    {
        var accept = ParseAccept(request.Headers.Accept);

        Match? literal = null, subtype = null, all = null;
        foreach (var (formatter, mediaTypes) in Formatters)
        {
            var match = MatchAccept(formatter, mediaTypes, accept);
            if (match is not { } m)
            {
                continue;
            }

            switch (m.Ranking)
            {
                case Ranking.Literal: literal = Better(literal, m); break;
                case Ranking.SubtypeMediaRange: subtype = Better(subtype, m); break;
                default: all = Better(all, m); break;
            }
        }

        var best = literal;
        if (subtype is { } s && (best is null || s.Quality > best.Value.Quality))
        {
            best = subtype;
        }

        if (all is { } a && (best is null || a.Quality > best.Value.Quality))
        {
            best = all;
        }

        if (best is { } b)
        {
            return new NegotiationResult(b.Formatter, b.MediaType);
        }

        if (MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
        {
            foreach (var (formatter, mediaTypes) in Formatters)
            {
                var supported = mediaTypes.FirstOrDefault(t => contentType.MediaType.Equals(t, StringComparison.OrdinalIgnoreCase));
                if (supported is not null)
                {
                    return new NegotiationResult(formatter, supported);
                }
            }
        }

        return new NegotiationResult(LegacyFormatter.Json, "application/json");
    }

    private static Match? Better(Match? current, Match candidate) =>
        current is null || candidate.Quality > current.Value.Quality ? candidate : current;

    private static Match? MatchAccept(LegacyFormatter formatter, string[] mediaTypes, IReadOnlyList<MediaTypeHeaderValue> accept)
    {
        foreach (var range in accept)
        {
            var quality = range.Quality ?? 1.0;
            if (quality <= 0.0)
            {
                continue;
            }

            foreach (var supported in mediaTypes)
            {
                var slash = supported.IndexOf('/');
                var type = supported[..slash];
                var rangeType = range.Type.Value ?? string.Empty;
                var rangeSubType = range.SubType.Value ?? string.Empty;

                if (rangeType == "*" && rangeSubType == "*")
                {
                    return new Match(formatter, supported, quality, Ranking.AllMediaRange);
                }

                if (rangeSubType == "*" && rangeType.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    return new Match(formatter, supported, quality, Ranking.SubtypeMediaRange);
                }

                if (range.MediaType.Equals(supported, StringComparison.OrdinalIgnoreCase))
                {
                    return new Match(formatter, supported, quality, Ranking.Literal);
                }
            }
        }

        return null;
    }

    /// <summary>Accept values ordered by quality, then specificity (literal before type/* before */*); stable otherwise.</summary>
    private static IReadOnlyList<MediaTypeHeaderValue> ParseAccept(IList<string> values)
    {
        if (!MediaTypeHeaderValue.TryParseList(values, out var parsed))
        {
            return Array.Empty<MediaTypeHeaderValue>();
        }

        return parsed
            .OrderByDescending(v => v.Quality ?? 1.0)
            .ThenByDescending(v => v.MatchesAllTypes ? 0 : v.MatchesAllSubTypes ? 1 : 2)
            .ToList();
    }
}
