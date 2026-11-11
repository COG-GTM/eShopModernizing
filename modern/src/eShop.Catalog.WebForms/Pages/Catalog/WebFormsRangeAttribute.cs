using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace eShop.Catalog.WebForms.Pages.Catalog;

public enum WebFormsValidationDataType
{
    Integer,
    Currency,
}

/// <summary>
/// Server-side port of the legacy <c>asp:RangeValidator</c> (Type="Integer" / Type="Currency", en-US):
/// empty input is valid (RangeValidator never fires on empty text), otherwise the text must convert with the
/// WebForms rules and fall inside [minimum, maximum].
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed partial class WebFormsRangeAttribute : ValidationAttribute
{
    public const string CurrencyDecimalChar = ".";
    public const string CurrencyGroupChar = ",";
    public const int CurrencyDigits = 2;
    public const int CurrencyGroupSize = 3;

    public WebFormsRangeAttribute(WebFormsValidationDataType type, string minimum, string maximum)
    {
        Type = type;
        Minimum = minimum;
        Maximum = maximum;
    }

    public WebFormsValidationDataType Type { get; }

    public string Minimum { get; }

    public string Maximum { get; }

    public override bool IsValid(object? value)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return TryConvert(text, Type, out var number)
            && TryConvert(Minimum, Type, out var min)
            && TryConvert(Maximum, Type, out var max)
            && number >= min
            && number <= max;
    }

    public static bool TryConvert(string text, WebFormsValidationDataType type, out decimal value)
    {
        value = 0;
        if (type == WebFormsValidationDataType.Integer)
        {
            var m = IntegerPattern().Match(text);
            if (!m.Success || !int.TryParse(m.Groups["sign"].Value + m.Groups["digits"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            {
                return false;
            }

            value = integer;
            return true;
        }

        var c = CurrencyPattern().Match(text);
        if (!c.Success)
        {
            return false;
        }

        var integerPart = c.Groups["int"].Value.Replace(CurrencyGroupChar, string.Empty, StringComparison.Ordinal);
        var fraction = c.Groups["frac"].Value;
        if (integerPart.Length == 0 && fraction.Length == 0)
        {
            return false;
        }

        var repaired = c.Groups["sign"].Value + (integerPart.Length == 0 ? "0" : integerPart) + (fraction.Length > 0 ? "." + fraction : string.Empty);
        return decimal.TryParse(repaired, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    [GeneratedRegex(@"^\s*(?<sign>[-+])?(?<digits>[0-9]+)\s*$")]
    private static partial Regex IntegerPattern();

    // BaseCompareValidator.ConvertCurrency / WebUIValidation.js: whitespace only before the decimal point, 0-2 fraction digits.
    [GeneratedRegex(@"^\s*(?<sign>[-+])?(?<int>[0-9]{1,3}(,[0-9]{3})+|[0-9]*)\s*(\.(?<frac>[0-9]{0,2}))?\s*$")]
    private static partial Regex CurrencyPattern();
}
