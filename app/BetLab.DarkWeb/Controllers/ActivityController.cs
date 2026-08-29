using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class ActivityController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public ActivityController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var wallet = await _apiService.GetMyWalletAsync(token);
            var bets = await _apiService.GetMyBetsAsync(token, userId);
            var transactions = await _apiService.GetMyWalletTransactionsAsync(token);

            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId,
                    "Dark",
                    "ActivityPageViewed",
                    "Activity",
                    "Page",
                    "ActivityIndex",
                    null,
                    null,
                    $"{{\"betCount\":\"{bets.Count}\",\"transactionCount\":\"{transactions.Count}\"}}");
            }

            var model = new MyActivityPageViewModel
            {
                Wallet = wallet,
                Bets = bets,
                Transactions = transactions
            };

            return View(model);
        }
    }
}