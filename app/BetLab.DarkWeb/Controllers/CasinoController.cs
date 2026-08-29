using System.Text.Json;
using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class CasinoController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public CasinoController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index(int? gameId = null)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var games = await _apiService.GetCasinoGamesAsync(token);
            var history = await _apiService.GetCasinoHistoryAsync(token);

            var selectedGame = gameId.HasValue
                ? games.FirstOrDefault(g => g.CasinoGameId == gameId.Value)
                : games.FirstOrDefault();

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Dark",
                    "CasinoPageViewed", "Casino", "Page", "CasinoIndex",
                    null, null,
                    selectedGame != null ? $"{{\"gameId\":\"{selectedGame.CasinoGameId}\"}}" : "{}");
            }

            return View(new CasinoPageViewModel
            {
                Games = games,
                SelectedGame = selectedGame,
                History = history
            });
        }

        [HttpPost]
        public async Task<IActionResult> SpinAjax(int casinoGameId, decimal stake)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return Unauthorized();

            var spinResult = await _apiService.SpinCasinoAsync(token, casinoGameId, stake);
            if (spinResult == null)
                return BadRequest("Rotirea a eșuat.");

            return Json(spinResult);
        }

        [HttpPost]
        public async Task<IActionResult> Spin(int casinoGameId, decimal stake)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var spinResult = await _apiService.SpinCasinoAsync(token, casinoGameId, stake);
            if (spinResult == null)
            {
                TempData["Error"] = "Rotirea a eșuat. Încearcă din nou!";
                return RedirectToAction("Index", new { gameId = casinoGameId });
            }

            TempData["CasinoLastSpin"] = JsonSerializer.Serialize(spinResult);

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Dark",
                    "CasinoSpinClicked", "Casino", "Button", "spin-button",
                    null, null,
                    $"{{\"casinoGameId\":\"{casinoGameId}\",\"stake\":\"{stake}\"}}");

                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Dark",
                    "CasinoRoundCompleted", "Casino", "Round", spinResult.CasinoRoundId.ToString(),
                    null, spinResult.ResultType,
                    $"{{\"multiplier\":\"{spinResult.Multiplier}\",\"payout\":\"{spinResult.Payout}\"}}");
            }

            return RedirectToAction("Index", new { gameId = casinoGameId });
        }
    }
}
