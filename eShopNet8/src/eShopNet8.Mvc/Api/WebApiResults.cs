using System.Runtime.Serialization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace eShopNet8.Mvc.Api;

// Reproduces the response conventions of the legacy ASP.NET Web API 2 pipeline that the brands API relied on:
// Json.NET-style PascalCase JSON, Accept-header content negotiation between the JSON and XML (DataContractSerializer)
// formatters (INVENTORY Q16), the HttpError body for failed Int32 parameter binding, and 405 responses with Allow.
public static class WebApiResults
{
    // Newtonsoft (Web API 2) wrote PascalCase names and did not \u-escape characters such as ' in HttpError messages.
    // Relaxed escaping is safe here because these bodies are always served as application/json, never inlined into HTML.
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string LegacyControllerType = "eShopLegacyMVC.Controllers.WebApi.BrandsController";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly XmlWriterSettings XmlSettings = new()
    {
        OmitXmlDeclaration = true,
        Encoding = Utf8NoBom,
        CloseOutput = false,
    };

    private enum Formatter { Json, Xml }

    // Web API 2 default formatter order and supported media types (JsonMediaTypeFormatter, then XmlMediaTypeFormatter).
    private static readonly (Formatter Formatter, string[] MediaTypes)[] Formatters =
    {
        (Formatter.Json, new[] { "application/json", "text/json" }),
        (Formatter.Xml, new[] { "application/xml", "text/xml" }),
    };

    public static IResult Negotiated<T>(HttpContext context, T value, int statusCode = StatusCodes.Status200OK) where T : notnull
        => Negotiated(context, statusCode, value, writeXml: writer => new DataContractSerializer(typeof(T)).WriteObject(writer, value));

    // Web API 2 answers a missing/non-Int32 {id} with 400 and an HttpError naming the legacy action signature.
    // The text is reproduced verbatim because it is part of the recorded contract (flow mvc-api-brand-nonnumeric).
    public static IResult InvalidInt32Parameter(HttpContext context, string action)
    {
        var detail = "The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' " +
                     $"for method 'System.Web.Http.IHttpActionResult {action}(Int32)' in '{LegacyControllerType}'. " +
                     "An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.";
        return HttpError(context, StatusCodes.Status400BadRequest, ("Message", "The request is invalid."), ("MessageDetail", detail));
    }

    // Web API 2 returned NotFound() / unknown-id DELETE as an empty-bodied 404; keep it empty even if the MVC pipeline
    // (T3) enables status code pages for the HTML site.
    public static IResult EmptyNotFound(HttpContext context)
    {
        var statusCodePages = context.Features.Get<IStatusCodePagesFeature>();
        if (statusCodePages is not null)
        {
            statusCodePages.Enabled = false;
        }
        return Results.NotFound();
    }

    public static IResult MethodNotAllowed(HttpContext context, params string[] allowed)
    {
        context.Response.Headers.Allow = new StringValues(allowed);
        return HttpError(context, StatusCodes.Status405MethodNotAllowed,
            ("Message", $"The requested resource does not support http method '{context.Request.Method}'."));
    }

    private static IResult HttpError(HttpContext context, int statusCode, params (string Name, string Value)[] fields)
    {
        var json = fields.ToDictionary(f => f.Name, f => f.Value);
        return Negotiated(context, statusCode, json, writeXml: writer =>
        {
            writer.WriteStartElement("Error");
            foreach (var (name, value) in fields)
            {
                writer.WriteElementString(name, value);
            }
            writer.WriteEndElement();
        });
    }

    private static IResult Negotiated(HttpContext context, int statusCode, object value, Action<XmlWriter> writeXml)
    {
        var (formatter, mediaType) = SelectFormatter(context.Request);
        byte[] body;
        if (formatter == Formatter.Xml)
        {
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, XmlSettings))
            {
                writeXml(writer);
            }
            body = stream.ToArray();
        }
        else
        {
            body = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), JsonOptions);
        }

        return new BytesResult(body, mediaType + "; charset=utf-8", statusCode);
    }

    // Port of System.Net.Http.Formatting.DefaultContentNegotiator: the best Accept match by quality (literal media types
    // beat type/* and */* ranges on equal quality, earlier formatters beat later ones), then the request Content-Type,
    // then the first formatter (JSON). Browsers sending "application/xml;q=0.9,*/*;q=0.8" therefore get XML, like legacy.
    private static (Formatter Formatter, string MediaType) SelectFormatter(HttpRequest request)
    {
        var accept = MediaTypeHeaderValue.TryParseList(request.Headers.Accept, out var parsed)
            ? parsed.OrderByDescending(a => a.Quality ?? 1.0).ThenByDescending(Specificity).ToList()
            : new List<MediaTypeHeaderValue>();

        (Formatter Formatter, string MediaType, double Quality)? literal = null, subtypeRange = null, allRange = null;
        foreach (var (formatter, mediaTypes) in Formatters)
        {
            var match = MatchAccept(accept, formatter, mediaTypes);
            if (match is null)
            {
                continue;
            }

            var (candidate, kind) = match.Value;
            switch (kind)
            {
                case 0: allRange = Better(allRange, candidate); break;
                case 1: subtypeRange = Better(subtypeRange, candidate); break;
                default: literal = Better(literal, candidate); break;
            }
        }

        var best = Better(Better(literal, subtypeRange), allRange);
        if (best is not null)
        {
            return (best.Value.Formatter, best.Value.MediaType);
        }

        if (MediaTypeHeaderValue.TryParse(request.ContentType, out var requestType))
        {
            foreach (var (formatter, mediaTypes) in Formatters)
            {
                var supported = mediaTypes.FirstOrDefault(m => requestType.MediaType.Equals(m, StringComparison.OrdinalIgnoreCase));
                if (supported is not null)
                {
                    return (formatter, supported);
                }
            }
        }

        return (Formatters[0].Formatter, Formatters[0].MediaTypes[0]);
    }

    private static ((Formatter, string, double) Match, int Kind)? MatchAccept(
        List<MediaTypeHeaderValue> sortedAccept, Formatter formatter, string[] mediaTypes)
    {
        foreach (var accept in sortedAccept)
        {
            var quality = accept.Quality ?? 1.0;
            if (quality <= 0)
            {
                continue;
            }

            foreach (var supported in mediaTypes)
            {
                if (accept.MatchesAllTypes)
                {
                    return ((formatter, supported, quality), 0);
                }
                if (accept.MatchesAllSubTypes && supported.StartsWith(accept.Type.Value + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return ((formatter, supported, quality), 1);
                }
                if (accept.MediaType.Equals(supported, StringComparison.OrdinalIgnoreCase))
                {
                    return ((formatter, supported, quality), 2);
                }
            }
        }

        return null;
    }

    private static int Specificity(MediaTypeHeaderValue value)
        => value.MatchesAllTypes ? 0 : value.MatchesAllSubTypes ? 1 : 2;

    private static (Formatter Formatter, string MediaType, double Quality)? Better(
        (Formatter Formatter, string MediaType, double Quality)? current,
        (Formatter Formatter, string MediaType, double Quality)? candidate)
    {
        if (candidate is null)
        {
            return current;
        }
        if (current is null)
        {
            return candidate;
        }
        return candidate.Value.Quality > current.Value.Quality ? candidate : current;
    }

    private sealed class BytesResult(byte[] body, string contentType, int statusCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = contentType;
            httpContext.Response.ContentLength = body.Length;
            return httpContext.Response.Body.WriteAsync(body, 0, body.Length);
        }
    }
}
