using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace eShopModernized.Tests.Common;

/// <summary>
/// Helpers for wiring up the minimum MVC infrastructure a <see cref="Controller"/>
/// needs in unit tests: an <see cref="HttpContext"/>, request services (for
/// authentication extension methods), <c>ViewData</c>/<c>ViewBag</c> and
/// <c>TempData</c>.
/// </summary>
public static class MvcTestContext
{
    public static void Configure(Controller controller, IAuthenticationService? authenticationService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>();

        var urlHelper = new Mock<IUrlHelper>();
        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(urlHelper.Object);
        services.AddSingleton(urlHelperFactory.Object);

        if (authenticationService != null)
        {
            services.AddSingleton(authenticationService);
        }

        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        controller.ViewData = new ViewDataDictionary(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary());

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
