using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
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
                HttpContext.Session.SetString("Dark_JwtToken", token);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}