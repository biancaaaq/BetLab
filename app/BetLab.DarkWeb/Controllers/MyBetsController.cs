using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class MyBetsController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public MyBetsController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index(string tab = "all")
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return RedirectToAction("Login", "Account");

            var bets = await _apiService.GetMyBetsAsync(token, userId);

            // Pre-fetch cash-out preview pentru fiecare bilet activ
            var previews = new Dictionary<int, CashOutPreviewViewModel>();
            var openBets = bets.Where(b => b.Status is "Open" or "Pending" or "Active").ToList();
            var previewTasks = openBets.Select(b =>
                _apiService.GetCashOutPreviewAsync(token, b.BetSlipId, userId)
                    .ContinueWith(t => (b.BetSlipId, t.Result)));
            var previewResults = await Task.WhenAll(previewTasks);
            foreach (var (id, preview) in previewResults)
            {
                if (preview != null) previews[id] = preview;
            }

            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (sessionId.HasValue)
            {
                await _apiService.LogEventAsync(sessionId.Value, userId, "Dark",
                    "MyBetsPageViewed", "MyBets", "Page", "MyBetsIndex", null, null,
                    $"{{\"tab\":\"{tab}\",\"betCount\":{bets.Count}}}");
            }

            var model = new MyBetsPageViewModel { Bets = bets, ActiveTab = tab, CashOutPreviews = previews };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CashOut(int betSlipId)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userIdString, out var userId))
                return RedirectToAction("Login", "Account");

            var (success, cashoutValue, newBalance, error) =
                await _apiService.CashOutAsync(token, betSlipId, userId);

            if (success && cashoutValue.HasValue)
            {
                TempData["CashOutSuccess"] =
                    $"Cash Out realizat! Ai primit {cashoutValue.Value:F2} RON în cont.";

                var sessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (sessionId.HasValue)
                {
                    await _apiService.LogEventAsync(sessionId.Value, userId, "Dark",
                        "CashOut", "MyBets", "BetSlip", betSlipId.ToString(),
                        null, cashoutValue.Value.ToString("F2"),
                        $"{{\"betSlipId\":{betSlipId},\"cashoutValue\":{cashoutValue.Value}}}");
                }
            }
            else
            {
                TempData["CashOutError"] = error ?? "Eroare la Cash Out.";
            }

            return RedirectToAction("Index", new { tab = "active" });
        }
    }
}
