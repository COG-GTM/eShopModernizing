using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShop.Catalog.Mvc.Html;

/// <summary>
/// Normalises ASP.NET Core HTML helper output to the byte-level shape System.Web.Mvc 5 produced:
/// attributes sorted ordinally (MVC 5) instead of case-insensitively (Core), validation state classes first
/// (MVC 5 prepends them, Core appends) and '+' left unencoded inside attribute values (Core emits <c>&amp;#x2B;</c>).
/// </summary>
public static partial class LegacyHtmlHelperExtensions
{
    private static readonly string[] StateClasses =
    {
        "field-validation-valid", "field-validation-error", "input-validation-error", "input-validation-valid",
        "validation-summary-valid", "validation-summary-errors",
    };

    public static IHtmlContent WithLegacyMarkup(this IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return new HtmlString(ToLegacyMarkup(writer.ToString()));
    }

    /// <summary>
    /// MVC 5 <c>ValidationSummary(true, ...)</c> renders an errors container (with a hidden placeholder item) whenever
    /// ModelState is invalid, even with no model-level errors; Core renders nothing in that case.
    /// </summary>
    public static IHtmlContent LegacyValidationSummary(this IHtmlHelper html, string cssClass)
    {
        var modelState = html.ViewData.ModelState;
        if (modelState.IsValid)
        {
            return HtmlString.Empty;
        }

        if (modelState.TryGetValue(string.Empty, out var entry) && entry.Errors.Count > 0)
        {
            return html.ValidationSummary(true, string.Empty, new { @class = cssClass }).WithLegacyMarkup();
        }

        return new HtmlString(
            $"<div class=\"validation-summary-errors {HtmlEncoder.Default.Encode(cssClass)}\"><ul><li style=\"display:none\"></li>\n</ul></div>");
    }

    public static string ToLegacyMarkup(string html)
    {
        return StartTag().Replace(html, m =>
        {
            var attributes = Attribute().Matches(m.Groups["attrs"].Value)
                .Select(a => (Name: a.Groups["name"].Value, Value: a.Groups["value"].Value))
                .Select(a => a.Name == "class" ? (a.Name, Value: LegacyClassOrder(a.Value)) : a)
                .OrderBy(a => a.Name, StringComparer.Ordinal)
                .Select(a => $" {a.Name}=\"{a.Value.Replace("&#x2B;", "+")}\"");
            var end = m.Groups["end"].Value.Length > 0 ? " />" : ">";
            return $"<{m.Groups["tag"].Value}{string.Concat(attributes)}{end}";
        });
    }

    public static string LegacyClassOrder(string classes)
    {
        var tokens = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', tokens.Where(StateClasses.Contains).Concat(tokens.Where(t => !StateClasses.Contains(t))));
    }

    [GeneratedRegex("<(?<tag>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>(?:\\s+[^\\s=/>]+=\"[^\"]*\")*)\\s*(?<end>/?)>")]
    private static partial Regex StartTag();

    [GeneratedRegex("(?<name>[^\\s=/>]+)=\"(?<value>[^\"]*)\"")]
    private static partial Regex Attribute();
}
