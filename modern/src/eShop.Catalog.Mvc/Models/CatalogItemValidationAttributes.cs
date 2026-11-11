using System.ComponentModel.DataAnnotations;

namespace eShop.Catalog.Mvc.Models;

/// <summary>
/// Server-side maximum length matching a column limit (e.g. <c>Catalog.Name nvarchar(50)</c>). Unlike
/// <see cref="StringLengthAttribute"/>/<see cref="MaxLengthAttribute"/> it has no client adapter, so the rendered
/// form keeps the legacy markup (no extra <c>data-val-*</c> attributes) while an over-long value re-renders the form
/// with a validation message instead of failing in the database.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ColumnMaxLengthAttribute : ValidationAttribute
{
    public ColumnMaxLengthAttribute(int length)
        : base("The field {0} must be a string or array type with a maximum length of '{1}'.")
    {
        Length = length;
    }

    public int Length { get; }

    public override bool IsValid(object? value) => value is not string text || text.Length <= Length;

    public override string FormatErrorMessage(string name) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, ErrorMessageString, name, Length);
}

/// <summary>
/// <see cref="RangeAttribute"/> with integer bounds that treats values outside <see cref="int"/> as out of range
/// instead of throwing <see cref="OverflowException"/> from <c>Convert.ToInt32</c> (a 500 in legacy too). In-range
/// behaviour, messages and the <c>data-val-range*</c> attributes are those of <see cref="RangeAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SafeRangeAttribute : RangeAttribute
{
    public SafeRangeAttribute(int minimum, int maximum)
        : base(minimum, maximum)
    {
    }

    public override bool IsValid(object? value)
    {
        try
        {
            return base.IsValid(value);
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
