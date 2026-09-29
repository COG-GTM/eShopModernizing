using Autofac;
using eShopModernizedMVC.Models;
using eShopModernizedMVC.Models.Infrastructure;
using eShopModernizedMVC.Services;

namespace eShopModernizedMVC.Modules
{
    public class ApplicationModule : Module
    {
        private bool useMockData;
        private bool useAzureStorage;
        private bool useManagedIdentity;
        private string picsPhysicalPath;

        public ApplicationModule(bool useMockData, bool useAzureStorage, bool useManagedIdentity, string picsPhysicalPath)
        {
            this.useMockData = useMockData;
            this.useAzureStorage = useAzureStorage;
            this.useManagedIdentity = useManagedIdentity;
            this.picsPhysicalPath = picsPhysicalPath;
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

            if (this.useAzureStorage)
            {
                builder.RegisterType<ImageAzureStorage>()
                    .As<IImageService>()
                    .WithParameter("picsPhysicalPath", picsPhysicalPath)
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<ImageMockStorage>()
                  .As<IImageService>()
                  .WithParameter("picsPhysicalPath", picsPhysicalPath)
                  .InstancePerLifetimeScope();
            }


            builder.RegisterType<CatalogDBContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CatalogDBInitializer>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CatalogItemHiLoGenerator>()
                .SingleInstance();

            if (this.useManagedIdentity)
            {
                builder.RegisterType<ManagedIdentitySqlConnectionFactory>()
                    .As<ISqlConnectionFactory>()
                    .SingleInstance();
            }
            else
            {
                builder.RegisterType<AppSettingsSqlConnectionFactory>()
                    .As<ISqlConnectionFactory>()
                    .SingleInstance();
            }
        }
    }
}