using BetLab.EthicalWeb.Services;
using BetLab.EthicalWeb.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BetLab.EthicalWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public HomeController(ILogger<HomeController> logger, ApiService apiService, EthicalSessionManager sessionManager)
        {
            _logger          = logger;
            _apiService      = apiService;
            _sessionManager  = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var homeUserId = _sessionManager.GetCurrentUserId();
            var homeSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (homeUserId.HasValue && homeSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    homeSessionId.Value, homeUserId.Value, "Ethical",
                    "HomeViewed", "Home", "Page", "HomeIndex",
                    null, null, "{}");
            }

            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Sold curent
                var wallet = await _apiService.GetMyWalletAsync(token);
                ViewBag.Balance = wallet?.Balance;

                // Statistici sesiune curentă (azi, UTC)
                var transactions = await _apiService.GetMyWalletTransactionsAsync(token);
                var today = DateTime.UtcNow.Date;

                var todayBetsLost = transactions
                    .Where(t => t.Type == "BetPlaced" && t.CreatedAt >= today)
                    .Sum(t => t.Amount);

                var todayWon = transactions
                    .Where(t => t.Type == "BetWon" && t.CreatedAt >= today)
                    .Sum(t => t.Amount);

                ViewBag.TodayStaked  = todayBetsLost;
                ViewBag.TodayNet     = todayWon - todayBetsLost;
                ViewBag.HasActivity  = transactions.Any(t => t.CreatedAt >= today);
            }

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
