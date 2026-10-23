namespace eShopNet8.Mvc.Hosting
{
    // Owned by ticket T6 (containerisation). Scaffold version: liveness only.
    public static class HealthChecksExtensions
    {
        public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHealthChecks();
            return services;
        }

        public static WebApplication MapCatalogHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks("/health");
            return app;
        }
    }
}
