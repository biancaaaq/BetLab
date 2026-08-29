using System.Text.Json;
using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class BlackjackController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public BlackjackController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public IActionResult Index()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
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
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
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
                await _apiService.LogEventAsync(expSessionId.Value, userId.Value, "Ethical",
                    "BlackjackStart", "Blackjack", "Button", "start",
                    null, null, $"{{\"stake\":\"{stake}\"}}");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Hit(long sessionId)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
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
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
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
                await _apiService.LogEventAsync(expSessionId.Value, userId.Value, "Ethical",
                    "BlackjackRoundCompleted", "Blackjack", "Round", state.SessionId.ToString(),
                    null, state.Status, $"{{\"payout\":\"{state.Payout}\",\"stake\":\"{state.Stake}\"}}");
            }

            return RedirectToAction("Index");
        }
    }
}
