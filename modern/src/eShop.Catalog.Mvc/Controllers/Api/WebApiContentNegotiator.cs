using Microsoft.Net.Http.Headers;

namespace eShop.Catalog.Mvc.Controllers.Api;

public enum LegacyWireFormat
{
    Json,
    Xml,
}

public sealed record NegotiatedFormat(LegacyWireFormat Format, string MediaType)
{
    public string ContentType => MediaType + "; charset=utf-8";
}

/// <summary>
/// Port of the Web API 2 DefaultContentNegotiator for its default formatters (JSON: application/json, text/json;
/// XML: application/xml, text/xml). Accept values are ordered by quality, then specificity; a literal match beats a
/// media-range match of equal quality; earlier formatters win ties; no match falls back to JSON (never 406).
/// So a browser's <c>text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8</c> gets XML, exactly like legacy.
/// </summary>
public static class WebApiContentNegotiator
{
    private static readonly (LegacyWireFormat Format, string[] MediaTypes)[] Formatters =
    {
        (LegacyWireFormat.Json, new[] { "application/json", "text/json" }),
        (LegacyWireFormat.Xml, new[] { "application/xml", "text/xml" }),
    };

    public static readonly NegotiatedFormat Default = new(LegacyWireFormat.Json, "application/json");

    public static NegotiatedFormat Negotiate(IEnumerable<string>? acceptHeaders)
    {
        var accepts = Parse(acceptHeaders)
            .Select((value, index) => (Value: value, Index: index))
            .OrderByDescending(a => a.Value.Quality ?? 1.0)
            .ThenByDescending(a => Specificity(a.Value))
            .ThenBy(a => a.Index)
            .Select(a => a.Value)
            .ToList();

        Match? bestLiteral = null;
        Match? bestRange = null;
        foreach (var (format, mediaTypes) in Formatters)
        {
            var match = MatchFormatter(format, mediaTypes, accepts);
            if (match is null)
            {
                continue;
            }

            if (match.IsLiteral)
            {
                bestLiteral = Better(bestLiteral, match);
            }
            else
            {
                bestRange = Better(bestRange, match);
            }
        }

        var best = Better(bestLiteral, bestRange);
        return best is null ? Default : new NegotiatedFormat(best.Format, best.MediaType);
    }

    private static Match? MatchFormatter(LegacyWireFormat format, string[] mediaTypes, List<MediaTypeHeaderValue> accepts)
    {
        foreach (var accept in accepts)
        {
            var quality = accept.Quality ?? 1.0;
            if (quality <= 0.0)
            {
                continue;
            }

            foreach (var supported in mediaTypes)
            {
                var slash = supported.IndexOf('/');
                var type = supported[..slash];
                var acceptType = accept.Type.Value ?? string.Empty;
                var acceptSubType = accept.SubType.Value ?? string.Empty;
                if (acceptType == "*" && acceptSubType == "*")
                {
                    return new Match(format, supported, quality, IsLiteral: false);
                }

                if (!string.Equals(acceptType, type, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (acceptSubType == "*")
                {
                    return new Match(format, supported, quality, IsLiteral: false);
                }

                if (string.Equals(acceptSubType, supported[(slash + 1)..], StringComparison.OrdinalIgnoreCase))
                {
                    return new Match(format, supported, quality, IsLiteral: true);
                }
            }
        }

        return null;
    }

    private static Match? Better(Match? current, Match? candidate)
    {
        if (candidate is null)
        {
            return current;
        }

        return current is null || candidate.Quality > current.Quality ? candidate : current;
    }

    private static int Specificity(MediaTypeHeaderValue value)
    {
        if (value.MatchesAllTypes)
        {
            return 0;
        }

        return value.MatchesAllSubTypes ? 1 : 2;
    }

    private static List<MediaTypeHeaderValue> Parse(IEnumerable<string>? acceptHeaders)
    {
        var result = new List<MediaTypeHeaderValue>();
        if (acceptHeaders is null)
        {
            return result;
        }

        foreach (var header in acceptHeaders)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            foreach (var part in header.Split(','))
            {
                if (MediaTypeHeaderValue.TryParse(part.Trim(), out var parsed))
                {
                    result.Add(parsed);
                }
            }
        }

        return result;
    }

    private sealed record Match(LegacyWireFormat Format, string MediaType, double Quality, bool IsLiteral);
}
