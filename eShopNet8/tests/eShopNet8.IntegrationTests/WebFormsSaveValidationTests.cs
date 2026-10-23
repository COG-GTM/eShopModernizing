extern alias PagesApp;

using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagesApp::eShopNet8.WebForms.Extensions;

namespace eShopNet8.IntegrationTests;

// SQL mode of the WebForms port: EF6 save-time validation of eShopLegacyWebForms.Models.CatalogItem (parity run S-mode,
// wf-create-postback-stock-out-of-range: legacy 500 and nothing stored; wf-create-postback-name-missing: legacy 302, stored).
public class WebFormsSaveValidationTests
{
    private static CatalogItem Item(Action<CatalogItem>? change = null)
    {
        var item = new CatalogItem { Name = "", Price = 1234567.89m, CatalogBrandId = 1, CatalogTypeId = 1, AvailableStock = 10000000 };
        change?.Invoke(item);
        return item;
    }

    [Fact]
    public void AddCatalogPages_registers_the_WebForms_rules_over_the_MVC_default()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICatalogItemSaveValidator, CatalogItemAnnotationsSaveValidator>();
        services.AddCatalogPages(new ConfigurationBuilder().Build());
        using var sp = services.BuildServiceProvider();
        Assert.IsType<WebFormsCatalogItemSaveValidator>(sp.GetRequiredService<ICatalogItemSaveValidator>());
        Assert.Single(services, s => s.ServiceType == typeof(ICatalogItemSaveValidator));
    }

    [Fact]
    public void Empty_name_and_wide_price_pass_like_the_legacy_WebForms_model()
    {
        new WebFormsCatalogItemSaveValidator().Validate(Item());
        Assert.Throws<ValidationException>(() => new CatalogItemAnnotationsSaveValidator().Validate(Item()));
    }

    [Theory]
    [InlineData(nameof(CatalogItem.AvailableStock))]
    [InlineData(nameof(CatalogItem.RestockThreshold))]
    [InlineData(nameof(CatalogItem.MaxStockThreshold))]
    [InlineData(nameof(CatalogItem.Price))]
    public void Out_of_range_values_fail(string property)
    {
        var item = Item(i =>
        {
            switch (property)
            {
                case nameof(CatalogItem.AvailableStock): i.AvailableStock = 10000001; break;
                case nameof(CatalogItem.RestockThreshold): i.RestockThreshold = -1; break;
                case nameof(CatalogItem.MaxStockThreshold): i.MaxStockThreshold = 20000000; break;
                default: i.Price = -1m; break;
            }
        });
        Assert.Throws<ValidationException>(() => new WebFormsCatalogItemSaveValidator().Validate(item));
    }
}
