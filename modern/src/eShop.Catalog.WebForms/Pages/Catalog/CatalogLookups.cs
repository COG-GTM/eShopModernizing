using System.Globalization;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>
/// Legacy DropDownList event validation: a posted value that is not one of the rendered options is rejected
/// ("Invalid postback or callback argument.", HTTP 500 in legacy) before any validator or click handler runs.
/// </summary>
public static class CatalogLookups
{
    public const string InvalidPostbackMessage = "Invalid postback or callback argument.";

    public static bool TryResolve(string? postedValue, IEnumerable<int> renderedIds, out int id)
    {
        id = 0;
        if (postedValue is null)
        {
            return false;
        }

        foreach (var candidate in renderedIds)
        {
            if (string.Equals(candidate.ToString(CultureInfo.InvariantCulture), postedValue, StringComparison.Ordinal))
            {
                id = candidate;
                return true;
            }
        }

        return false;
    }

    public static int Resolve(string? postedValue, IEnumerable<int> renderedIds)
    {
        return TryResolve(postedValue, renderedIds, out var id) ? id : throw new ArgumentException(InvalidPostbackMessage);
    }
}
