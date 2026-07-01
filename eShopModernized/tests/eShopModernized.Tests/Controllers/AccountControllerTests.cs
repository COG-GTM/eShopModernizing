using System.Security.Claims;
using eShopCoreModernized.Controllers;
using eShopModernized.Tests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers;

public class AccountControllerTests
{
    private static AccountController CreateController(Mock<IAuthenticationService>? auth = null, IUrlHelper? url = null)
    {
        var controller = new AccountController();
        MvcTestContext.Configure(controller, (auth ?? new Mock<IAuthenticationService>()).Object);
        if (url != null)
        {
            controller.Url = url;
        }
        return controller;
    }

    [Fact]
    public void Login_Get_ReturnsView_AndSetsReturnUrl()
    {
        var controller = CreateController();

        var result = controller.Login("/back");

        Assert.IsType<ViewResult>(result);
        Assert.Equal("/back", controller.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Post_InvalidCredentials_ReturnsViewWithError()
    {
        var controller = CreateController();

        var result = await controller.Login(string.Empty, string.Empty, "/back");

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Invalid username or password", controller.ViewData["Error"]);
        Assert.Equal("/back", controller.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Post_Valid_LocalReturnUrl_Redirects()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl("/home")).Returns(true);

        var controller = CreateController(auth, url.Object);

        var result = await controller.Login("user", "pass", "/home");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), "Cookies",
            It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_Valid_NonLocalReturnUrl_RedirectsToCatalog()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(false);

        var controller = CreateController(auth, url.Object);

        var result = await controller.Login("user", "pass", "http://evil.com");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_Valid_NoReturnUrl_RedirectsToCatalog()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(auth);

        var result = await controller.Login("user", "pass");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
    }

    [Fact]
    public async Task Logout_SignsOut_AndRedirectsToCatalog()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(auth);

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Catalog", redirect.ControllerName);
        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), "Cookies",
            It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public void AccessDenied_ReturnsView()
    {
        var controller = CreateController();

        Assert.IsType<ViewResult>(controller.AccessDenied());
    }
}
