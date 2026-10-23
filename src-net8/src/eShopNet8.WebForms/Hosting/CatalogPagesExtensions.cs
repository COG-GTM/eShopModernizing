namespace eShopNet8.WebForms.Hosting
{
    // Owned by ticket T5 (WebForms -> Razor Pages). Scaffold stub: replace the bodies, keep the signatures.
    public static class CatalogPagesExtensions
    {
        public static IServiceCollection AddCatalogPages(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRazorPages();
            return services;
        }

        public static WebApplication UseCatalogPages(this WebApplication app)
        {
            app.UseStaticFiles();
            app.UseRouting();
            return app;
        }

        public static WebApplication MapCatalogPages(this WebApplication app)
        {
            app.MapRazorPages();
            return app;
        }
    }
}
