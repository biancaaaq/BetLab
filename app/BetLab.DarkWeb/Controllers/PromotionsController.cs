using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class PromotionsController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public PromotionsController(ApiService apiService, DarkSessionManager sessionManager)
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
                await _apiService.LogEventAsync(sessionId.Value, userId.Value, "Dark",
                    "PageViewed", "Promotions", "Page", "PromotionsIndex",
                    null, null, null);
            }
            return View();
        }

        
        /// Revendică bonusul zilnic (dark pattern: daily login reward).
        /// Se poate revendica o singură dată pe sesiune (session key Dark_BonusClaimedToday).
        
        [HttpPost]
        public async Task<IActionResult> ClaimDailyBonus()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return RedirectToAction("Login", "Account");

            // Verifică dacă deja a revendicat astăzi
            var claimKey = $"Dark_DailyBonus_{DateTime.UtcNow:yyyyMMdd}_{userId}";
            if (HttpContext.Session.GetString(claimKey) == "claimed")
            {
                TempData["PromoError"] = "Ai revendicat deja bonusul zilnic astăzi. Revino mâine!";
                return RedirectToAction("Index");
            }

            // Adaugă 10 RON via topup-demo
            var (success, newBalance, error) = await _apiService.DepositAsync(token, 10m);
            if (success)
            {
                HttpContext.Session.SetString(claimKey, "claimed");
                TempData["PromoSuccess"] = $"🎁 Bonus zilnic de 10 RON adăugat! Sold nou: {newBalance:F2} RON";

                var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (sessionId.HasValue)
                {
                    await _apiService.LogEventAsync(sessionId.Value, userId, "Dark",
                        "BonusClaimed", "Promotions", "Promo", "DailyBonus",
                        null, "10", null);
                }
            }
            else
            {
                TempData["PromoError"] = error ?? "Nu s-a putut revendica bonusul.";
            }

            return RedirectToAction("Index");
        }

        
        //  MYSTERY WHEEL — "Învârte roata și primește o surpriză!"
        //  Dark pattern: VARIABLE RATIO REINFORCEMENT SCHEDULE (Skinner, 1948),
        //  cel mai adictiv program de întărire din psihologia operantă.
        //  Recompensa este aleatoare și imprevizibilă; include "near-miss"
        //  ("Aproape!") care întreține comportamentul de joc.
        //  Ordinea segmentelor TREBUIE să corespundă vizualului din
        //  Views/Promotions/Index.cshtml (același index 0..7).
        
        private static readonly (string Label, decimal Amount, int Weight)[] WheelSegments =
        {
            ("5 RON",          5m,   22), // 0
            ("50 RON 🎉",      50m,   5), // 1
            ("2 RON",          2m,   25), // 2
            ("JACKPOT 100 💎", 100m,  2), // 3 — rar (jackpot)
            ("10 RON",         10m,  16), // 4
            ("Aproape! 😬",    0m,   13), // 5 — near-miss (nimic)
            ("20 RON",         20m,   9), // 6
            ("5 rotiri 🎁",    5m,    8), // 7
        };

        
        /// Învârte roata norocului. Recompensă aleatoare (variable ratio),
        /// o singură dată pe zi per utilizator. Backend-ul decide segmentul
        /// (anti-cheat + logging consistent), frontend-ul doar animează.
        
        [HttpPost]
        public async Task<IActionResult> SpinWheel()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return Json(new { success = false, requiresLogin = true });

            var spinKey = $"Dark_WheelSpin_{DateTime.UtcNow:yyyyMMdd}_{userId}";
            if (HttpContext.Session.GetString(spinKey) == "spun")
                return Json(new { success = false, alreadySpun = true,
                    message = "Ai învârtit deja roata azi. Revino mâine pentru o nouă surpriză!" });

            // Selecție ponderată — variable ratio: recompensa e imprevizibilă.
            var totalWeight = 0;
            foreach (var s in WheelSegments) totalWeight += s.Weight;
            var roll = Random.Shared.Next(totalWeight);
            var index = 0;
            for (var i = 0; i < WheelSegments.Length; i++)
            {
                if (roll < WheelSegments[i].Weight) { index = i; break; }
                roll -= WheelSegments[i].Weight;
            }

            var segment = WheelSegments[index];

            // Marcăm spin-ul ca efectuat indiferent de rezultat (o dată pe zi).
            HttpContext.Session.SetString(spinKey, "spun");

            decimal? newBalance = null;
            if (segment.Amount > 0m)
            {
                var (success, balance, _) = await _apiService.DepositAsync(token, segment.Amount);
                if (success) newBalance = balance;
            }

            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (sessionId.HasValue)
            {
                await _apiService.LogEventAsync(sessionId.Value, userId, "Dark",
                    "WheelSpin", "Promotions", "Promo", "MysteryWheel",
                    null, segment.Amount.ToString("0.##"), index.ToString());
            }

            return Json(new
            {
                success     = true,
                segmentIndex = index,
                label       = segment.Label,
                amount      = segment.Amount,
                newBalance  = newBalance,
                isNearMiss  = segment.Amount == 0m
            });
        }

        
        /// Revendică free bet-ul de 50 RON (o singură dată per sesiune).
        
        [HttpPost]
        public async Task<IActionResult> ClaimFreeBet()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return RedirectToAction("Login", "Account");

            var claimKey = $"Dark_FreeBet_{userId}";
            if (HttpContext.Session.GetString(claimKey) == "claimed")
            {
                TempData["PromoError"] = "Free bet-ul a fost deja revendicat!";
                return RedirectToAction("Index");
            }

            var (success, newBalance, error) = await _apiService.DepositAsync(token, 50m);
            if (success)
            {
                HttpContext.Session.SetString(claimKey, "claimed");
                TempData["PromoSuccess"] = $"🎯 Free Bet de 50 RON activat! Sold nou: {newBalance:F2} RON";

                var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (sessionId.HasValue)
                {
                    await _apiService.LogEventAsync(sessionId.Value, userId, "Dark",
                        "BonusClaimed", "Promotions", "Promo", "FreeBet",
                        null, "50", null);
                }
            }
            else
            {
                TempData["PromoError"] = error ?? "Eroare la activare free bet.";
            }

            return RedirectToAction("Index");
        }
    }
}
