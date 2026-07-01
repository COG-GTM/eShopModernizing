using Autofac;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Services;

namespace eShopLegacyMVC.Modules
{
    public class ApplicationModule : Module
    {
        private bool useMockData;
        private bool useCustomizationData;
        private string connectionString;
        private string contentRootPath;
        private string webRootPath;

        public ApplicationModule(bool useMockData, bool useCustomizationData, string connectionString, string contentRootPath, string webRootPath)
        {
            this.useMockData = useMockData;
            this.useCustomizationData = useCustomizationData;
            this.connectionString = connectionString;
            this.contentRootPath = contentRootPath;
            this.webRootPath = webRootPath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (this.useMockData)
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

            builder.Register(c => new CatalogDBContext(connectionString))
                .InstancePerLifetimeScope();

            builder.Register(c => new CatalogDBInitializer(
                    c.Resolve<CatalogItemHiLoGenerator>(),
                    useCustomizationData,
                    contentRootPath,
                    webRootPath))
                .InstancePerLifetimeScope();

            builder.RegisterType<CatalogItemHiLoGenerator>()
                .SingleInstance();
        }
    }
}
