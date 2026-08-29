using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class ActivityController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public ActivityController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            var userIdString = HttpContext.Session.GetString("Ethical_CurrentUserId");

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
                    "Ethical",
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