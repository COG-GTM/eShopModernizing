using Autofac;
using eShopLegacyMVC.Configuration;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Services;

namespace eShopLegacyMVC.Modules
{
    public class ApplicationModule : Module
    {
        private readonly ICatalogSettings catalogSettings;

        public ApplicationModule(ICatalogSettings catalogSettings)
        {
            this.catalogSettings = catalogSettings;
        }
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(catalogSettings)
                .As<ICatalogSettings>()
                .SingleInstance();

            if (catalogSettings.UseMockData)
            {
                builder.RegisterType<CatalogServiceMock>()
                    .As<ICatalogService>()
                    .SingleInstance();
            }
            else
            {
                builder.RegisterType<CatalogService>()
                    .As<ICatalogService>()
                    .InstancePerLifetimeScope();
            }

            builder.RegisterType<CatalogDBContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CatalogDBInitializer>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CatalogItemHiLoGenerator>()
                .SingleInstance();
        }
    }
}