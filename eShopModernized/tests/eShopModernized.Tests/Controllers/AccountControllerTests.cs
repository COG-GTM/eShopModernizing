using System.Security.Claims;
using eShopCoreModernized.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<IAuthenticationService> _authService = new();

        private AccountController CreateController()
        {
            var tempDataFactory = new Mock<ITempDataDictionaryFactory>();
            tempDataFactory.Setup(f => f.GetTempData(It.IsAny<HttpContext>()))
                .Returns(new Mock<ITempDataDictionary>().Object);

            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IAuthenticationService)))
                .Returns(_authService.Object);
            services.Setup(s => s.GetService(typeof(ITempDataDictionaryFactory)))
                .Returns(tempDataFactory.Object);

            var httpContext = new DefaultHttpContext
            {
                RequestServices = services.Object
            };

            return new AccountController
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                Url = CreateUrlHelper("/local").Object
            };
        }

        private static Mock<IUrlHelper> CreateUrlHelper(string localUrl)
        {
            var url = new Mock<IUrlHelper>();
            url.Setup(u => u.IsLocalUrl(localUrl)).Returns(true);
            url.Setup(u => u.IsLocalUrl(It.Is<string>(s => s != localUrl))).Returns(false);
            return url;
        }

        [Fact]
        public void Login_Get_SetsReturnUrlAndReturnsView()
        {
            var controller = CreateController();

            var result = controller.Login("/somewhere");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("/somewhere", view.ViewData["ReturnUrl"]);
        }

        [Fact]
        public async Task Login_Post_ValidCredentials_LocalReturnUrl_SignsInAndRedirects()
        {
            var controller = CreateController();

            var result = await controller.Login("user", "pass", "/local");

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/local", redirect.Url);
            _authService.Verify(s => s.SignInAsync(
                It.IsAny<HttpContext>(),
                "Cookies",
                It.Is<ClaimsPrincipal>(p => p.Identity!.Name == "user"),
                It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        [Fact]
        public async Task Login_Post_ValidCredentials_NoReturnUrl_RedirectsToCatalogIndex()
        {
            var controller = CreateController();

            var result = await controller.Login("user", "pass");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Catalog", redirect.ControllerName);
            _authService.Verify(s => s.SignInAsync(
                It.IsAny<HttpContext>(),
                "Cookies",
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        [Fact]
        public async Task Login_Post_ValidCredentials_NonLocalReturnUrl_RedirectsToCatalogIndex()
        {
            var controller = CreateController();

            var result = await controller.Login("user", "pass", "http://evil.com");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Catalog", redirect.ControllerName);
        }

        [Theory]
        [InlineData("", "pass")]
        [InlineData("user", "")]
        [InlineData("", "")]
        public async Task Login_Post_MissingCredentials_SetsErrorAndReturnsView(string username, string password)
        {
            var controller = CreateController();

            var result = await controller.Login(username, password, "/back");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("/back", view.ViewData["ReturnUrl"]);
            Assert.Equal("Invalid username or password", view.ViewData["Error"]);
            _authService.Verify(s => s.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()), Times.Never);
        }

        [Fact]
        public async Task Logout_SignsOutAndRedirectsToCatalogIndex()
        {
            var controller = CreateController();

            var result = await controller.Logout();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Catalog", redirect.ControllerName);
            _authService.Verify(s => s.SignOutAsync(
                It.IsAny<HttpContext>(),
                "Cookies",
                It.IsAny<AuthenticationProperties>()), Times.Once);
        }

        [Fact]
        public void AccessDenied_ReturnsView()
        {
            var controller = CreateController();

            var result = controller.AccessDenied();

            Assert.IsType<ViewResult>(result);
        }
    }
}
