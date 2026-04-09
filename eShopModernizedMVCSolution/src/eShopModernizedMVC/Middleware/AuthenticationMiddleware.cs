using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace eShopModernizedMVC.Middleware
{
    public class AuthenticationMiddleware : OwinMiddleware
    {
        private static readonly ClaimsIdentity SharedIdentity = CreateIdentity();

        private static ClaimsIdentity CreateIdentity()
        {
            var identity = new ClaimsIdentity("cookies");
            identity.AddClaim(new Claim("iat", "1234"));
            return identity;
        }

        public AuthenticationMiddleware(OwinMiddleware next)
        : base(next)
        {
        }

        public async override Task Invoke(IOwinContext context)
        {
            var principal = new ClaimsPrincipal(SharedIdentity);
            context.Authentication.User = principal;
            await Next.Invoke(context);
        }
    }
}
