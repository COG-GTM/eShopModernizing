using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>
/// Renders legacy validator spans the way ASP.NET 4.5 WebForms <c>BaseValidator.Render</c> does with Display="Dynamic".
/// Uplevel (script-capable browser, unobtrusive mode): always rendered with data-val-* expandos, hidden with
/// <c>display:none;</c> while valid; the expandos drive the script in <c>_CatalogItemFormValidation.cshtml</c>.
/// Downlevel (any other client): nothing while valid, a plain span with the message once server validation fails.
/// </summary>
public static class WebFormsValidatorMarkup
{
    public static IHtmlContent RequiredField(string id, string controlToValidate, string cssClass, string message, bool isValid, bool renderUplevel)
    {
        if (!renderUplevel)
        {
            return Downlevel(id, cssClass, message, isValid);
        }

        var sb = Open(id, controlToValidate, cssClass, message, isValid);
        Attr(sb, "data-val-evaluationfunction", "RequiredFieldValidatorEvaluateIsValid");
        Attr(sb, "data-val-initialvalue", string.Empty);
        return Close(sb, message, isValid);
    }

    public static IHtmlContent Range(string id, string controlToValidate, string cssClass, string message, WebFormsValidationDataType type, string minimum, string maximum, bool isValid, bool renderUplevel)
    {
        if (!renderUplevel)
        {
            return Downlevel(id, cssClass, message, isValid);
        }

        var sb = Open(id, controlToValidate, cssClass, message, isValid);
        Attr(sb, "data-val-type", type.ToString());
        if (type == WebFormsValidationDataType.Currency)
        {
            Attr(sb, "data-val-decimalchar", WebFormsRangeAttribute.CurrencyDecimalChar);
            Attr(sb, "data-val-groupchar", WebFormsRangeAttribute.CurrencyGroupChar);
            Attr(sb, "data-val-digits", WebFormsRangeAttribute.CurrencyDigits.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Attr(sb, "data-val-groupsize", WebFormsRangeAttribute.CurrencyGroupSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Attr(sb, "data-val-evaluationfunction", "RangeValidatorEvaluateIsValid");
        Attr(sb, "data-val-maximumvalue", maximum);
        Attr(sb, "data-val-minimumvalue", minimum);
        return Close(sb, message, isValid);
    }

    private static IHtmlContent Downlevel(string id, string cssClass, string message, bool isValid)
    {
        if (isValid)
        {
            return HtmlString.Empty;
        }

        var sb = new StringBuilder("<span");
        Attr(sb, "id", id);
        Attr(sb, "class", cssClass);
        sb.Append('>').Append(HtmlEncoder.Default.Encode(message)).Append("</span>");
        return new HtmlString(sb.ToString());
    }

    private static StringBuilder Open(string id, string controlToValidate, string cssClass, string message, bool isValid)
    {
        var sb = new StringBuilder("<span");
        Attr(sb, "data-val-controltovalidate", controlToValidate);
        Attr(sb, "data-val-errormessage", message);
        Attr(sb, "data-val-display", "Dynamic");
        if (!isValid)
        {
            Attr(sb, "data-val-isvalid", "False");
        }

        Attr(sb, "id", id);
        Attr(sb, "class", cssClass);
        Attr(sb, "data-val", "true");
        return sb;
    }

    private static HtmlString Close(StringBuilder sb, string message, bool isValid)
    {
        if (isValid)
        {
            Attr(sb, "style", "display:none;");
        }

        sb.Append('>').Append(HtmlEncoder.Default.Encode(message)).Append("</span>");
        return new HtmlString(sb.ToString());
    }

    private static void Attr(StringBuilder sb, string name, string value)
    {
        sb.Append(' ').Append(name).Append("=\"").Append(HtmlEncoder.Default.Encode(value)).Append('"');
    }
}
