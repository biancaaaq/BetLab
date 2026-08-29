using System.Text.Json;
using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class BlackjackController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public BlackjackController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public IActionResult Index()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            BlackjackStateViewModel? state = null;
            var raw = TempData["BlackjackState"]?.ToString();
            if (raw != null)
                state = JsonSerializer.Deserialize<BlackjackStateViewModel>(raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return View(new BlackjackPageViewModel { State = state });
        }

        [HttpPost]
        public async Task<IActionResult> Start(decimal stake)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var state = await _apiService.BlackjackStartAsync(token, stake);
            if (state == null)
            {
                TempData["Error"] = "Nu s-a putut porni jocul. Verifică soldul.";
                return RedirectToAction("Index");
            }

            TempData["BlackjackState"] = JsonSerializer.Serialize(state);

            var userId = _sessionManager.GetCurrentUserId();
            var expSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && expSessionId.HasValue)
            {
                await _apiService.LogEventAsync(expSessionId.Value, userId.Value, "Dark",
                    "BlackjackStart", "Blackjack", "Button", "start",
                    null, null, $"{{\"stake\":\"{stake}\"}}");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Hit(long sessionId)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var state = await _apiService.BlackjackHitAsync(token, sessionId);
            if (state == null)
            {
                TempData["Error"] = "Acțiunea a eșuat.";
                return RedirectToAction("Index");
            }

            TempData["BlackjackState"] = JsonSerializer.Serialize(state);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Stand(long sessionId)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login", "Account");

            var state = await _apiService.BlackjackStandAsync(token, sessionId);
            if (state == null)
            {
                TempData["Error"] = "Acțiunea a eșuat.";
                return RedirectToAction("Index");
            }

            TempData["BlackjackState"] = JsonSerializer.Serialize(state);

            var userId = _sessionManager.GetCurrentUserId();
            var expSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && expSessionId.HasValue)
            {
                await _apiService.LogEventAsync(expSessionId.Value, userId.Value, "Dark",
                    "BlackjackRoundCompleted", "Blackjack", "Round", state.SessionId.ToString(),
                    null, state.Status, $"{{\"payout\":\"{state.Payout}\",\"stake\":\"{state.Stake}\"}}");
            }

            return RedirectToAction("Index");
        }
    }
}
