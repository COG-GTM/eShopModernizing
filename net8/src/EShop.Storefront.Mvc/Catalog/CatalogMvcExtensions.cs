// Owned by T2 (MVC catalog UI).
using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace EShop.Storefront.Mvc.Catalog;

public static class CatalogMvcExtensions
{
    // Legacy Web.config had no <globalization>; the IIS host ran en-US, which drives "$19.50" and decimal model binding.
    private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    public static IServiceCollection AddCatalogMvc(this IServiceCollection services)
    {
        // MVC 5 had no implicit [Required] for strings (e.g. the read-only PictureFileName on Edit).
        services.AddControllersWithViews(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        services.Configure<Microsoft.AspNetCore.Mvc.MvcViewOptions>(options =>
            options.ClientModelValidatorProviders.Add(new IntegerNumberClientValidatorProvider()));
        services.AddAntiforgery();
        LegacyXFrameOptionsAntiforgery.Decorate(services);
        return services;
    }

    public static WebApplication MapCatalogMvc(this WebApplication app)
    {
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(LegacyCulture),
            SupportedCultures = new[] { LegacyCulture },
            SupportedUICultures = new[] { LegacyCulture },
            RequestCultureProviders = new List<IRequestCultureProvider>(),
        });

        app.MapControllerRoute(
            name: "Default",
            pattern: "{controller=Catalog}/{action=Index}/{id?}");

        return app;
    }
}
