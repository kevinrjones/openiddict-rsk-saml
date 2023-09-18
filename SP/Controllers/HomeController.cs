using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Rsk.AspNetCore.Authentication.Saml2p.Tests.Host.Net6.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult LoginIdentityServer() => Challenge(new AuthenticationProperties { RedirectUri = "/" }, "saml-identityServer");
        public IActionResult LoginOpenIddict() => Challenge(new AuthenticationProperties { RedirectUri = "/" }, "saml-openIddict");
        public IActionResult LoginCs() => Challenge(new AuthenticationProperties { RedirectUri = "/" }, "saml-cs");
        public IActionResult Logout() => new SignOutResult(new[] { "cookie", User.Identities.First().AuthenticationType });
    }
}