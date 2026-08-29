using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    
    /// Pagină de promoții ETICĂ — afișează ofertele clar, fără urgență fabricată sau dark patterns.
    /// Fiecare promoție are T&C transparent, fără rulaj ascuns.
    
    public class PromotionsController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public PromotionsController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService     = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _sessionManager.GetCurrentUserId();
            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && sessionId.HasValue)
            {
                await _apiService.LogEventAsync(sessionId.Value, userId.Value, "Ethical",
                    "PageViewed", "Promotions", "Page", "PromotionsIndex",
                    null, null, null);
            }
            return View();
        }

        
        /// Ethical variant: bonus zilnic TRANSPARENT și FIX — 5 RON demo.
        /// Suma este afișată clar înainte de revendicare. Fără mistere, fără spin,
        /// fără near-miss. Design: previsibilitate = control = comportament sănătos.
        /// Contrast cu Dark: aceeași valoare medie (~5-10 RON) dar fără variabilitate
        /// — tocmai variabilitatea este mecanismul adictiv, nu suma în sine.
        
        [HttpPost]
        public async Task<IActionResult> ClaimDailyReward()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            var userIdString = HttpContext.Session.GetString("Ethical_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return RedirectToAction("Login", "Account");

            var claimKey = $"Ethical_DailyReward_{DateTime.UtcNow:yyyyMMdd}_{userId}";
            if (HttpContext.Session.GetString(claimKey) == "claimed")
            {
                TempData["PromoError"] = "Ai revendicat deja bonusul de 5 RON azi. Revino mâine!";
                return RedirectToAction("Index");
            }

            const decimal DAILY_AMOUNT = 5m;   // sumă fixă, transparentă, identică în fiecare zi
            var (success, newBalance, error) = await _apiService.DepositAsync(token, DAILY_AMOUNT);

            if (success)
            {
                HttpContext.Session.SetString(claimKey, "claimed");
                TempData["PromoSuccess"] = $"Bonus zilnic de {DAILY_AMOUNT:F0} RON adăugat. Sold nou: {newBalance:F2} RON.";

                var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (sessionId.HasValue)
                {
                    await _apiService.LogEventAsync(sessionId.Value, userId, "Ethical",
                        "BonusClaimed", "Promotions", "Promo", "DailyFixed",
                        null, DAILY_AMOUNT.ToString("0.##"), null);
                }
            }
            else
            {
                TempData["PromoError"] = error ?? "Nu s-a putut revendica bonusul.";
            }

            return RedirectToAction("Index");
        }
    }
}
