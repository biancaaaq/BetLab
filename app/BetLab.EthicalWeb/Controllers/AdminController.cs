using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApiService _apiService;

        public AdminController(ApiService apiService)
        {
            _apiService = apiService;
        }

        private string? GetAdminToken()
        {
            var isAdmin = HttpContext.Session.GetString("Ethical_IsAdmin");
            if (isAdmin != "true") return null;
            return HttpContext.Session.GetString("Ethical_JwtToken");
        }

        private IActionResult AccessDenied()
        {
            TempData["AdminError"] = "Acces restricționat. Trebuie să fii autentificat ca administrator.";
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var dashboard = await _apiService.AdminGetDashboardAsync(token)
                ?? new AdminDashboardViewModel();

            return View(dashboard);
        }

        [HttpGet]
        public async Task<IActionResult> Events()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var events = await _apiService.AdminGetEventsAsync(token);
            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> AddEvent()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var model = new AdminAddEventViewModel
            {
                Sports       = await _apiService.AdminGetSportsAsync(token),
                Competitions = await _apiService.AdminGetCompetitionsAsync(token),
                Teams        = await _apiService.AdminGetTeamsAsync(token)
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddEvent(
            int sportId, int competitionId,
            int homeTeamId, int awayTeamId,
            DateTime startTimeUtc,
            string homeTeamName, string awayTeamName,
            decimal oddsHome, decimal oddsDraw, decimal oddsAway)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminCreateEventAsync(
                token, sportId, competitionId,
                homeTeamId, awayTeamId, startTimeUtc.ToUniversalTime(),
                homeTeamName, awayTeamName,
                oddsHome, oddsDraw, oddsAway);

            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Eveniment adăugat cu succes."
                : "Eroare la adăugarea evenimentului.";

            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> ImportApiFootball(int leagueId, int season, string name,
            string country = "Europe", string? dateFrom = null, string? dateTo = null,
            bool onlyUpcoming = true)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var (ok, msg) = await _apiService.AdminImportApiFootballAsync(
                token, leagueId, season, name, country, dateFrom, dateTo, onlyUpcoming);
            TempData[ok ? "AdminSuccess" : "AdminError"] = msg;
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> SyncLiveApiFootball()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var (ok, msg) = await _apiService.AdminSyncLiveApiFootballAsync(token);
            TempData[ok ? "AdminSuccess" : "AdminError"] = msg;
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> ImportApiFootballFixture(int fixtureId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            if (fixtureId <= 0)
            {
                TempData["AdminError"] = "Fixture ID invalid.";
                return RedirectToAction("Events");
            }

            var (ok, msg) = await _apiService.AdminImportApiFootballFixtureAsync(token, fixtureId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = msg;
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> SyncEvents(bool autoSettle = false)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var (ok, msg) = await _apiService.AdminSyncEventsAsync(token, autoSettle);
            TempData[ok ? "AdminSuccess" : "AdminError"] = msg;
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> MarkLive(int eventId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            await _apiService.AdminMarkEventLiveAsync(token, eventId, true);
            TempData["AdminSuccess"] = "Eveniment marcat ca LIVE.";
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> MarkOpen(int eventId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            await _apiService.AdminMarkEventLiveAsync(token, eventId, false);
            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> MarkWinningOutcome(int outcomeId, int eventId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminMarkWinningOutcomeAsync(token, outcomeId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Cotă câștigătoare marcată."
                : "Eroare la marcarea cotei câștigătoare.";

            return RedirectToAction("Events");
        }

        [HttpPost]
        public async Task<IActionResult> SettleEvent(int eventId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminSettleEventAsync(token, eventId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Eveniment decontat cu succes. Pariurile au fost procesate."
                : "Eroare la decontare. Verificați dacă cotele câștigătoare au fost marcate.";

            return RedirectToAction("Events");
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var users = await _apiService.AdminGetUsersAsync(token);
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ResetBalance(Guid userId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminResetBalanceAsync(token, userId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Soldul a fost resetat la 100 RON."
                : "Eroare la resetarea soldului.";

            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> BlockUser(Guid userId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminBlockUserAsync(token, userId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Utilizator blocat."
                : "Eroare la blocarea utilizatorului.";

            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> UnblockUser(Guid userId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminUnblockUserAsync(token, userId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Utilizator deblocat."
                : "Eroare la deblocarea utilizatorului.";

            return RedirectToAction("Users");
        }

        [HttpGet]
        public async Task<IActionResult> Casino()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var games = await _apiService.AdminGetCasinoGamesAsync(token);
            return View(games);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleGame(int gameId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            await _apiService.AdminToggleCasinoGameAsync(token, gameId);
            return RedirectToAction("Casino");
        }

        [HttpGet]
        public async Task<IActionResult> Transactions(string? type = null, int page = 1)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var model = await _apiService.AdminGetTransactionsAsync(token, type, page)
                ?? new AdminTransactionsPageViewModel();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SelfExclusion()
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var exclusions    = await _apiService.AdminGetExclusionsAsync(token);
            var users         = await _apiService.AdminGetUsersAsync(token);
            var cnpExclusions = await _apiService.AdminGetCnpExclusionsAsync(token);

            ViewBag.Users         = users;
            ViewBag.CnpExclusions = cnpExclusions;
            return View(exclusions);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCnpExclusion(long cnpId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminRemoveCnpExclusionAsync(token, cnpId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "CNP eliminat din Registrul de Autoexcludere."
                : "Eroare la eliminarea CNP-ului.";

            return RedirectToAction("SelfExclusion");
        }

        [HttpPost]
        public async Task<IActionResult> ImposeExclusion(Guid userId, string reason, bool permanent, string? excludedUntilStr)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            DateTime? excludedUntil = null;
            if (!permanent && DateTime.TryParse(excludedUntilStr, out var dt))
                excludedUntil = dt.ToUniversalTime();

            var ok = await _apiService.AdminImposeExclusionAsync(token, userId, reason, permanent, excludedUntil);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Excludere impusă cu succes."
                : "Eroare la impunerea excluderii.";

            return RedirectToAction("SelfExclusion");
        }

        [HttpPost]
        public async Task<IActionResult> LiftExclusion(long exclusionId)
        {
            var token = GetAdminToken();
            if (token == null) return AccessDenied();

            var ok = await _apiService.AdminLiftExclusionAsync(token, exclusionId);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "Excludere ridicată cu succes."
                : "Eroare la ridicarea excluderii.";

            return RedirectToAction("SelfExclusion");
        }

    }
}
