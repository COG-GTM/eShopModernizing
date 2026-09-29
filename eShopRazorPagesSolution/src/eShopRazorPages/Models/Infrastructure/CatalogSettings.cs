namespace eShopRazorPages.Models.Infrastructure;

/// <summary>
/// Mirrors the legacy <c>Web.config</c> appSettings. Keys are read from the configuration root so the same
/// environment variables used by the legacy containers (<c>UseMockData</c>, <c>UseCustomizationData</c>)
/// keep working.
/// </summary>
public class CatalogSettings
{
    public bool UseMockData { get; set; } = true;

    public bool UseCustomizationData { get; set; }
}
