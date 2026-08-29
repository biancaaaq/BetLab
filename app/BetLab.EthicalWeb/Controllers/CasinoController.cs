using System.Text.Json;
using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class CasinoController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public CasinoController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index(int? gameId = null)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var games = await _apiService.GetCasinoGamesAsync(token);
            var history = await _apiService.GetCasinoHistoryAsync(token);
            var wallet = await _apiService.GetMyWalletAsync(token);

            var selectedGame = gameId.HasValue
                ? games.FirstOrDefault(g => g.CasinoGameId == gameId.Value)
                : games.FirstOrDefault();

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId.Value,
                    "Ethical",
                    "CasinoPageViewed",
                    "Casino",
                    "Page",
                    "CasinoIndex",
                    null,
                    null,
                    selectedGame != null ? $"{{\"gameId\":\"{selectedGame.CasinoGameId}\"}}" : "{}");
            }

            return View(new CasinoPageViewModel
            {
                Games = games,
                SelectedGame = selectedGame,
                History = history,
                Balance = wallet?.Balance ?? 0m,
                Currency = wallet?.Currency ?? "RON"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Spin(int casinoGameId, decimal stake)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var (spinResult, spinError) = await _apiService.SpinCasinoAsync(token, casinoGameId, stake);
            if (spinResult == null)
            {
                TempData["Error"] = spinError ?? "Rotirea a eșuat. Verifică soldul și încearcă din nou.";
                return RedirectToAction("Index", new { gameId = casinoGameId });
            }

            TempData["CasinoLastSpin"] = JsonSerializer.Serialize(spinResult);

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId.Value,
                    "Ethical",
                    "CasinoSpinClicked",
                    "Casino",
                    "Button",
                    "spin-button",
                    null,
                    null,
                    $"{{\"casinoGameId\":\"{casinoGameId}\",\"stake\":\"{stake}\"}}");

                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId.Value,
                    "Ethical",
                    "CasinoRoundCompleted",
                    "Casino",
                    "Round",
                    spinResult.CasinoRoundId.ToString(),
                    null,
                    spinResult.ResultType,
                    $"{{\"multiplier\":\"{spinResult.Multiplier}\",\"payout\":\"{spinResult.Payout}\"}}");
            }

            return RedirectToAction("Index", new { gameId = casinoGameId });
        }
    }
}
