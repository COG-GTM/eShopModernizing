using Microsoft.Net.Http.Headers;

namespace eShopNet8.Mvc.Api
{
    public enum LegacyApiFormat
    {
        Json,
        Xml
    }

    /// <summary>
    /// Web API 2's <c>DefaultContentNegotiator</c> for the two default writers, in registration order:
    /// JsonMediaTypeFormatter (application/json, text/json), then XmlMediaTypeFormatter (application/xml, text/xml).
    /// Accept values are sorted by q (then specificity); each formatter takes its first match; the best literal,
    /// subtype-range and all-range matches compete on q only (ties keep that order, then formatter order); without an
    /// Accept match the request Content-Type decides, else JSON. Accept-Charset is ignored (always utf-8).
    /// </summary>
    public static class LegacyContentNegotiator
    {
        private enum Ranking
        {
            Literal,
            SubtypeRange,
            AllRange
        }

        private sealed record Match(LegacyApiFormat Format, string MediaType, double Quality, Ranking Ranking);

        private static readonly (LegacyApiFormat Format, string[] MediaTypes)[] Formatters =
        {
            (LegacyApiFormat.Json, new[] { "application/json", "text/json" }),
            (LegacyApiFormat.Xml, new[] { "application/xml", "text/xml" }),
        };

        public static (LegacyApiFormat Format, string MediaType) Negotiate(HttpRequest request)
        {
            var accept = ParseAccept(request.Headers.Accept);
            Match literal = null, subtype = null, all = null;
            foreach (var (format, mediaTypes) in Formatters)
            {
                var match = MatchAccept(accept, format, mediaTypes);
                switch (match?.Ranking)
                {
                    case Ranking.Literal: literal = Better(literal, match); break;
                    case Ranking.SubtypeRange: subtype = Better(subtype, match); break;
                    case Ranking.AllRange: all = Better(all, match); break;
                }
            }

            var best = Better(Better(literal, subtype), all);
            if (best != null)
            {
                return (best.Format, best.MediaType);
            }

            if (MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
            {
                foreach (var (format, mediaTypes) in Formatters)
                {
                    foreach (var mediaType in mediaTypes)
                    {
                        if (contentType.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase))
                        {
                            return (format, mediaType);
                        }
                    }
                }
            }

            return (LegacyApiFormat.Json, "application/json");
        }

        private static Match Better(Match current, Match candidate)
        {
            if (candidate == null)
            {
                return current;
            }
            if (current == null)
            {
                return candidate;
            }
            return candidate.Quality > current.Quality ? candidate : current;
        }

        private static Match MatchAccept(IReadOnlyList<MediaTypeHeaderValue> accept, LegacyApiFormat format, string[] mediaTypes)
        {
            foreach (var value in accept)
            {
                double quality = value.Quality ?? 1.0;
                if (quality == 0.0)
                {
                    continue;
                }

                foreach (var mediaType in mediaTypes)
                {
                    var supported = MediaTypeHeaderValue.Parse(mediaType);
                    if (value.MatchesAllTypes)
                    {
                        return new Match(format, mediaType, quality, Ranking.AllRange);
                    }
                    if (!supported.Type.Equals(value.Type, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (value.MatchesAllSubTypes)
                    {
                        return new Match(format, mediaType, quality, Ranking.SubtypeRange);
                    }
                    if (supported.SubType.Equals(value.SubType, StringComparison.OrdinalIgnoreCase))
                    {
                        return new Match(format, mediaType, quality, Ranking.Literal);
                    }
                }
            }
            return null;
        }

        private static IReadOnlyList<MediaTypeHeaderValue> ParseAccept(IList<string> headerValues)
        {
            if (headerValues == null || headerValues.Count == 0
                || !MediaTypeHeaderValue.TryParseList(headerValues, out var parsed))
            {
                return Array.Empty<MediaTypeHeaderValue>();
            }

            return parsed
                .OrderByDescending(v => v.Quality ?? 1.0)
                .ThenByDescending(v => v.MatchesAllTypes ? 0 : v.MatchesAllSubTypes ? 1 : 2)
                .ToList();
        }
    }
}
