using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using EShop.Catalog.Contracts;

namespace EShop.Storefront.Mvc.Api;

/// <summary>Writes Web API 2-shaped responses: Json.NET-style PascalCase JSON or DataContractSerializer-shaped XML.</summary>
internal static class LegacyWebApiResponse
{
    private const string DataContractNamespace = "http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models";
    private const string XmlSchemaInstanceNamespace = "http://www.w3.org/2001/XMLSchema-instance";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly XmlWriterSettings XmlSettings = new()
    {
        OmitXmlDeclaration = true,
        Encoding = Utf8NoBom,
    };

    public static void AddNoCacheHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "-1";
    }

    public static Task WriteBrandsAsync(HttpContext context, IReadOnlyList<CatalogBrand> brands) =>
        WriteNegotiatedAsync(context, StatusCodes.Status200OK,
            () => JsonSerializer.SerializeToUtf8Bytes(brands.Select(b => new { b.Id, b.Brand }), JsonOptions),
            writer =>
            {
                writer.WriteStartElement("ArrayOfCatalogBrand", DataContractNamespace);
                writer.WriteAttributeString("xmlns", "i", null, XmlSchemaInstanceNamespace);
                foreach (var brand in brands)
                {
                    writer.WriteStartElement("CatalogBrand", DataContractNamespace);
                    WriteBrandMembers(writer, brand);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            });

    public static Task WriteBrandAsync(HttpContext context, CatalogBrand brand) =>
        WriteNegotiatedAsync(context, StatusCodes.Status200OK,
            () => JsonSerializer.SerializeToUtf8Bytes(new { brand.Id, brand.Brand }, JsonOptions),
            writer =>
            {
                writer.WriteStartElement("CatalogBrand", DataContractNamespace);
                writer.WriteAttributeString("xmlns", "i", null, XmlSchemaInstanceNamespace);
                WriteBrandMembers(writer, brand);
                writer.WriteEndElement();
            });

    /// <summary>Web API <c>HttpError</c>: <c>{"Message","MessageDetail"}</c> or <c>&lt;Error&gt;&lt;Message/&gt;...</c>.</summary>
    public static Task WriteErrorAsync(HttpContext context, int status, string message, string? messageDetail = null) =>
        WriteNegotiatedAsync(context, status,
            () => messageDetail is null
                ? JsonSerializer.SerializeToUtf8Bytes(new { Message = message }, JsonOptions)
                : JsonSerializer.SerializeToUtf8Bytes(new { Message = message, MessageDetail = messageDetail }, JsonOptions),
            writer =>
            {
                writer.WriteStartElement("Error");
                writer.WriteElementString("Message", message);
                if (messageDetail is not null)
                {
                    writer.WriteElementString("MessageDetail", messageDetail);
                }

                writer.WriteEndElement();
            });

    public static Task WriteMethodNotAllowedAsync(HttpContext context, string[] allow)
    {
        // Web API emits one Allow header per supported method.
        context.Response.Headers.Allow = allow;
        return WriteErrorAsync(context, StatusCodes.Status405MethodNotAllowed,
            $"The requested resource does not support http method '{context.Request.Method}'.");
    }

    /// <summary>Content-less <c>HttpResponseMessage</c>: System.Web still reports its default text/html content type.</summary>
    public static Task WriteEmptyAsync(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html";
        context.Response.ContentLength = 0;
        return Task.CompletedTask;
    }

    private static void WriteBrandMembers(XmlWriter writer, CatalogBrand brand)
    {
        // DataContractSerializer orders members alphabetically: Brand, Id.
        writer.WriteStartElement("Brand", DataContractNamespace);
        if (brand.Brand is null)
        {
            writer.WriteAttributeString("nil", XmlSchemaInstanceNamespace, "true");
        }
        else
        {
            writer.WriteString(brand.Brand);
        }

        writer.WriteEndElement();
        writer.WriteElementString("Id", DataContractNamespace, XmlConvert.ToString(brand.Id));
    }

    private static async Task WriteNegotiatedAsync(HttpContext context, int status, Func<byte[]> json, Action<XmlWriter> xml)
    {
        var negotiated = LegacyContentNegotiator.Negotiate(context.Request);
        byte[] body;
        if (negotiated.Formatter == LegacyFormatter.Xml)
        {
            using var buffer = new MemoryStream();
            using (var writer = XmlWriter.Create(buffer, XmlSettings))
            {
                xml(writer);
            }

            body = buffer.ToArray();
        }
        else
        {
            body = json();
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = negotiated.MediaType + "; charset=utf-8";
        context.Response.ContentLength = body.Length;
        await context.Response.Body.WriteAsync(body);
    }
}
