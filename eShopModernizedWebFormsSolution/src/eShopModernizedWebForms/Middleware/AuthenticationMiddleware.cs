using Microsoft.Owin;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eShopModernizedWebForms.Middleware
{
    public class AuthenticationMiddleware : OwinMiddleware
    {
        private static readonly ClaimsPrincipal SharedPrincipal = CreatePrincipal();

        private static ClaimsPrincipal CreatePrincipal()
        {
            var identity = new ClaimsIdentity("cookies");
            identity.AddClaim(new Claim("iat", "1234"));
            var principal = new ClaimsPrincipal();
            principal.AddIdentity(identity);
            return principal;
        }

        public AuthenticationMiddleware(OwinMiddleware next)
        : base(next)
        {
        }

        public async override Task Invoke(IOwinContext context)
        {
            context.Authentication.User = SharedPrincipal;
            await Next.Invoke(context);
        }
    }
}
