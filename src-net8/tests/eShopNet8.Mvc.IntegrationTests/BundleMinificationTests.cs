using eShop.Catalog.Infrastructure.Data;
using eShopNet8.Mvc.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eShopNet8.Mvc.IntegrationTests
{
    /// <summary>System.Web.Optimization served every bundle minified (D5-bundles).</summary>
    public class BundleMinificationTests : IClassFixture<MvcAppFactory>
    {
        private readonly MvcAppFactory factory;

        public BundleMinificationTests(MvcAppFactory factory)
        {
            this.factory = factory;
        }

        [Theory]
        [InlineData("/bundles/jquery", "text/javascript", "jQuery", new[] { "Scripts/jquery-3.3.1.js" })]
        [InlineData("/bundles/jqueryval", "text/javascript", "validator", new[] { "Scripts/jquery.validate.js", "Scripts/jquery.validate.unobtrusive.js" })]
        [InlineData("/bundles/modernizr", "text/javascript", "Modernizr", new[] { "Scripts/modernizr-2.6.2.js", "Scripts/modernizr-2.8.3.js" })]
        [InlineData("/bundles/bootstrap", "text/javascript", "Bootstrap", new[] { "Scripts/bootstrap.js", "Scripts/respond.js" })]
        [InlineData("/Content/css", "text/css", ".navbar", new[] { "Content/bootstrap.css", "Content/custom.css", "Content/base.css", "Content/site.css" })]
        public async Task Bundle_is_minified_and_keeps_its_content(string path, string mediaType, string marker, string[] sources)
        {
            var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;
            var sourceLength = sources.Sum(s => webRoot.GetFileInfo(s).Length);

            var response = await factory.CreateClient().GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain("Minification failed", body);
            Assert.Contains(marker, body);
            Assert.True(body.Length < sourceLength * 0.85, $"{path}: {body.Length} chars vs {sourceLength} bytes of sources");
            Assert.DoesNotContain("\n\n", body);
        }

        [Fact]
        public async Task Bundle_is_rebuilt_when_a_source_file_changes()
        {
            var root = Directory.CreateTempSubdirectory("bundle-").FullName;
            try
            {
                File.WriteAllText(Path.Combine(root, "a.js"), "function first ( value ) { return value + 1 ; }");
                using var files = new PhysicalFileProvider(root);
                var bundle = new CatalogMvcExtensions.CachedBundle(files, new[] { "a.js" }, "text/javascript");
                var before = bundle.Content;
                Assert.Same(before, bundle.Content);

                File.WriteAllText(Path.Combine(root, "a.js"), "function second ( value ) { return value + 2 ; }");
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (bundle.Content == before && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(200);
                }

                Assert.Contains("second", bundle.Content);
                Assert.DoesNotContain(" ( ", bundle.Content);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Mvc_registers_no_save_validator()
        {
            Assert.Null(factory.Services.GetService<ICatalogSaveValidator>());
        }
    }
}
