using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class AuthController : Controller
    {
        [HttpGet]
        public IActionResult SetToken()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SetToken(string token)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                HttpContext.Session.SetString("Ethical_JwtToken", token);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}