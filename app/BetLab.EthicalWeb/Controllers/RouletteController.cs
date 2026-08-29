using System.Text.Json;
using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class RouletteController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public RouletteController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            RouletteSpinResponseViewModel? lastSpin = null;
            if (TempData["RouletteLastSpin"] != null)
            {
                lastSpin = JsonSerializer.Deserialize<RouletteSpinResponseViewModel>(
                    TempData["RouletteLastSpin"]!.ToString()!);
            }

            var history = await _apiService.GetRouletteHistoryAsync(token);

            var userId = _sessionManager.GetCurrentUserId();
            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && sessionId.HasValue)
            {
                await _apiService.LogEventAsync(sessionId.Value, userId.Value, "Ethical",
                    "RoulettePageViewed", "Roulette", "Page", "RouletteIndex", null, null, "{}");
            }

            return View(new RoulettePageViewModel { LastSpin = lastSpin, History = history });
        }

        [HttpPost]
        public async Task<IActionResult> Spin(string betType, string betValue, decimal stake)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(betType) || string.IsNullOrWhiteSpace(betValue))
            {
                TempData["Error"] = "Selectează un tip de pariu înainte de a roti.";
                return RedirectToAction("Index");
            }

            var result = await _apiService.RouletteSpinAsync(token, betType, betValue, stake);
            if (result == null)
            {
                TempData["Error"] = "Spin eșuat. Verifică soldul sau încearcă din nou.";
                return RedirectToAction("Index");
            }

            TempData["RouletteLastSpin"] = JsonSerializer.Serialize(result);

            var userId = _sessionManager.GetCurrentUserId();
            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && sessionId.HasValue)
            {
                await _apiService.LogEventAsync(sessionId.Value, userId.Value, "Ethical",
                    "RouletteSpinPlaced", "Roulette", "Button", "spin-button", null, null,
                    JsonSerializer.Serialize(new { betType, betValue, stake, result.ResultNumber, result.IsWin }));
            }

            return RedirectToAction("Index");
        }
    }
}
