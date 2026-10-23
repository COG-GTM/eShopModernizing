namespace eShopNet8.Mvc.Hosting
{
    // Owned by ticket T3 (MVC catalog UI). Scaffold stub: replace the bodies, keep the signatures.
    public static class CatalogMvcExtensions
    {
        public static IServiceCollection AddCatalogMvc(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllersWithViews();
            return services;
        }

        public static WebApplication UseCatalogMvc(this WebApplication app)
        {
            app.UseStaticFiles();
            app.UseRouting();
            return app;
        }

        public static WebApplication MapCatalogMvc(this WebApplication app)
        {
            app.MapControllerRoute("Default", "{controller=Catalog}/{action=Index}/{id?}");
            return app;
        }
    }
}
