using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Writes a response the way the legacy Web API 2 pipeline did: content negotiated between JSON and XML
/// (<see cref="WebApiContentNegotiator"/>), <c>Cache-Control: no-cache</c> / <c>Pragma: no-cache</c> / <c>Expires: -1</c>
/// on every response, optional <c>Allow</c> headers, and an empty body (labelled <c>text/html</c>, like legacy) when there
/// is no value. Status-code pages are switched off so an empty 404 stays empty whatever error handling the host adds.
/// </summary>
public sealed class LegacyWebApiResult : IActionResult
{
    private static readonly XmlWriterSettings XmlSettings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OmitXmlDeclaration = true,
        CloseOutput = false,
        CheckCharacters = false,
    };

    public LegacyWebApiResult(int statusCode, object? value = null, NegotiatedFormat? forcedFormat = null, IReadOnlyList<string>? allow = null)
    {
        StatusCode = statusCode;
        Value = value;
        ForcedFormat = forcedFormat;
        Allow = allow ?? Array.Empty<string>();
    }

    public int StatusCode { get; }

    public object? Value { get; }

    /// <summary>When set, skips content negotiation (used by /api/files, which is always JSON).</summary>
    public NegotiatedFormat? ForcedFormat { get; }

    public IReadOnlyList<string> Allow { get; }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var statusCodePages = http.Features.Get<IStatusCodePagesFeature>();
        if (statusCodePages is not null)
        {
            statusCodePages.Enabled = false;
        }

        var response = http.Response;
        response.StatusCode = StatusCode;
        response.Headers.CacheControl = "no-cache";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "-1";
        foreach (var method in Allow)
        {
            response.Headers.Append("Allow", method);
        }

        if (Value is null)
        {
            response.ContentType = "text/html";
            response.ContentLength = 0;
            return;
        }

        var format = ForcedFormat ?? WebApiContentNegotiator.Negotiate(http.Request.Headers.Accept);
        var body = Serialize(Value, format.Format);
        response.ContentType = format.ContentType;
        response.ContentLength = body.Length;
        await response.Body.WriteAsync(body, http.RequestAborted);
    }

    public static byte[] Serialize(object value, LegacyWireFormat format)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (format == LegacyWireFormat.Json)
        {
            if (value is LegacyHttpError error)
            {
                var fields = new Dictionary<string, string> { ["Message"] = error.Message };
                if (error.MessageDetail is not null)
                {
                    fields["MessageDetail"] = error.MessageDetail;
                }

                return JsonSerializer.SerializeToUtf8Bytes(fields, LegacyWebApiJson.SerializerOptions);
            }

            return JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), LegacyWebApiJson.SerializerOptions);
        }

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, XmlSettings))
        {
            if (value is LegacyHttpError error)
            {
                writer.WriteStartElement("Error");
                writer.WriteElementString("Message", error.Message);
                if (error.MessageDetail is not null)
                {
                    writer.WriteElementString("MessageDetail", error.MessageDetail);
                }

                writer.WriteEndElement();
            }
            else
            {
                new DataContractSerializer(value.GetType()).WriteObject(writer, value);
            }
        }

        return stream.ToArray();
    }
}
