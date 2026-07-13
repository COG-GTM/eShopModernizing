using System.Security.Claims;
using eShopCoreModernized.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace eShopModernized.Tests;

public class AccountControllerTests
{
    private readonly Mock<IAuthenticationService> _authService = new();

    private AccountController CreateController(bool treatReturnUrlAsLocal = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authService.Object);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(treatReturnUrlAsLocal);
        controller.Url = urlHelper.Object;

        return controller;
    }

    [Fact]
    public void Login_Get_ReturnsViewWithReturnUrlInViewData()
    {
        var controller = CreateController();

        var result = controller.Login("/protected");

        Assert.IsType<ViewResult>(result);
        Assert.Equal("/protected", controller.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Post_WithValidCredentials_SignsInAndRedirectsToCatalog()
    {
        var controller = CreateController();

        var result = await controller.Login("alice", "secret");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
        _authService.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(),
            "Cookies",
            It.Is<ClaimsPrincipal>(p => p.HasClaim(ClaimTypes.Name, "alice")),
            It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_WithLocalReturnUrl_RedirectsToReturnUrl()
    {
        var controller = CreateController(treatReturnUrlAsLocal: true);

        var result = await controller.Login("alice", "secret", "/dashboard");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    [Fact]
    public async Task Login_Post_WithNonLocalReturnUrl_RedirectsToCatalog()
    {
        var controller = CreateController(treatReturnUrlAsLocal: false);

        var result = await controller.Login("alice", "secret", "http://evil.example.com");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("alice", "")]
    [InlineData("", "")]
    public async Task Login_Post_WithMissingCredentials_ReturnsViewWithError(string username, string password)
    {
        var controller = CreateController();

        var result = await controller.Login(username, password, "/back");

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Invalid username or password", controller.ViewData["Error"]);
        Assert.Equal("/back", controller.ViewData["ReturnUrl"]);
        _authService.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Fact]
    public async Task Logout_SignsOutAndRedirectsToCatalog()
    {
        var controller = CreateController();

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
        _authService.Verify(a => a.SignOutAsync(
            It.IsAny<HttpContext>(), "Cookies", It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public void AccessDenied_ReturnsView()
    {
        var result = CreateController().AccessDenied();

        Assert.IsType<ViewResult>(result);
    }
}
