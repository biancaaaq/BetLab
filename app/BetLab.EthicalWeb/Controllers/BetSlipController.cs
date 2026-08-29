using BetLab.EthicalWeb.Helpers;
using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class BetSlipController : Controller
    {
        private const string BetSlipSessionKey = "Ethical_BetSlip";
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public BetSlipController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        public async Task<IActionResult> Index()
        {
            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();

            var model = new BetSlipViewModel
            {
                Items = items,
                Stake = 0,
                TotalOdds = CalculateTotalOdds(items),
                PotentialPayout = 0
            };

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId.Value,
                    "Ethical",
                    "PageViewed",
                    "BetSlip",
                    "Page",
                    "BetSlipIndex",
                    null,
                    null,
                    $"{{\"itemCount\":\"{items.Count}\"}}");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddOutcome(int outcomeId, string? ret = null)
        {
            // Abordare etică: dacă nu e autentificat, NU adăugăm tăcut în coș.
            // Setăm un mesaj informativ scurt și rămânem pe pagina curentă (Events list).
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Ethical_JwtToken")))
            {
                TempData["AuthInfo"] = "Autentifică-te sau creează un cont nou.";
                return ret == "Events"
                    ? RedirectToAction("Index", "Events")
                    : RedirectToAction("Index", "Events");
            }

            var events = await _apiService.GetEventsAsync();
            foreach (var ev in events)
            {
                var details = await _apiService.GetEventByIdAsync(ev.EventId);
                if (details == null) continue;
                foreach (var market in details.Markets)
                {
                    var outcome = market.Outcomes.FirstOrDefault(o => o.OutcomeId == outcomeId);
                    if (outcome == null) continue;

                    var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
                    var alreadyThis = items.Any(x => x.OutcomeId == outcomeId);

                    if (!alreadyThis)
                    {
                        var newItem = new BetSlipItemViewModel
                        {
                            OutcomeId   = outcome.OutcomeId,
                            EventId     = details.EventId,
                            EventName   = $"{details.HomeTeam} vs {details.AwayTeam}",
                            MarketId    = market.MarketId,
                            MarketName  = market.Name,
                            OutcomeName = outcome.Name,
                            Odds        = outcome.Odds,
                            MarketType  = market.MarketType,
                            OutcomeCode = outcome.Code
                        };

                        var otherItems = items
                            .Where(x => !(x.EventId == details.EventId && x.MarketId == market.MarketId))
                            .ToList();

                        var conflict = BetSlipConflictChecker.GetConflict(newItem, otherItems);
                        if (conflict != null)
                        {
                            TempData["Error"] = $"Selecție blocată: {conflict}";
                            HttpContext.Session.SetObject(BetSlipSessionKey, items);
                            return ret == "Events"
                                ? RedirectToAction("Index", "Events")
                                : RedirectToAction("Index");
                        }

                        items.RemoveAll(x => x.EventId == details.EventId && x.MarketId == market.MarketId);
                        items.Add(newItem);
                    }
                    else
                    {
                        items.RemoveAll(x => x.EventId == details.EventId && x.MarketId == market.MarketId);
                    }

                    HttpContext.Session.SetObject(BetSlipSessionKey, items);

                    return ret == "Events"
                        ? RedirectToAction("Index", "Events")
                        : RedirectToAction("Index");
                }
            }
            return ret == "Events"
                ? RedirectToAction("Index", "Events")
                : RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Add(
            int outcomeId,
            int eventId,
            string eventName,
            int marketId,
            string marketName,
            string outcomeName,
            decimal odds,
            string marketType = "",
            string outcomeCode = "",
            string? ret = null,
            int? returnId = null)
        {
            // Abordare etică: dacă nu e autentificat, NU adăugăm tăcut cota în sesiune
            // și NU îi forțăm Register-ul. Îi spunem clar și factual ce e necesar.
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Ethical_JwtToken")))
            {
                TempData["AuthInfo"] = "Autentifică-te sau creează un cont nou.";
                return ret switch
                {
                    "Events"  => RedirectToAction("Index", "Events"),
                    "Details" => RedirectToAction("Details", "Events", new { id = returnId ?? eventId }),
                    _         => RedirectToAction("Index", "Events")
                };
            }

            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();

            // O singură selecție per (event, market) — înlocuim dacă există alta
            var existingForMarket = items.FirstOrDefault(x => x.EventId == eventId && x.MarketId == marketId);
            bool alreadyThisOutcome = existingForMarket?.OutcomeId == outcomeId;

            if (!alreadyThisOutcome)
            {
                var newItem = new BetSlipItemViewModel
                {
                    OutcomeId   = outcomeId,  EventId     = eventId,     EventName   = eventName,
                    MarketId    = marketId,   MarketName  = marketName,  OutcomeName = outcomeName,
                    Odds        = odds,       MarketType  = marketType,  OutcomeCode = outcomeCode
                };

                var otherItems = items
                    .Where(x => !(x.EventId == eventId && x.MarketId == marketId))
                    .ToList();

                var conflict = BetSlipConflictChecker.GetConflict(newItem, otherItems);
                if (conflict != null)
                {
                    TempData["Error"] = $"Selecție blocată: {conflict}";
                    HttpContext.Session.SetObject(BetSlipSessionKey, items);
                    return ret switch
                    {
                        "Events"  => RedirectToAction("Index", "Events"),
                        "Details" => RedirectToAction("Details", "Events", new { id = returnId ?? eventId }),
                        _         => RedirectToAction("Index")
                    };
                }

                if (existingForMarket != null) items.Remove(existingForMarket);
                items.Add(newItem);

                HttpContext.Session.SetObject(BetSlipSessionKey, items);

                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(
                        experimentSessionId.Value,
                        userId.Value,
                        "Ethical",
                        "OutcomeSelected",
                        "EventDetails",
                        "Outcome",
                        outcomeId.ToString(),
                        null,
                        outcomeName,
                        $"{{\"eventName\":\"{eventName}\",\"marketName\":\"{marketName}\",\"odds\":\"{odds}\"}}");
                }
            }
            else
            {
                // Toggle off — deselectează aceeași cotă
                if (existingForMarket != null) items.Remove(existingForMarket);
                HttpContext.Session.SetObject(BetSlipSessionKey, items);
            }

            return ret switch
            {
                "Events"  => RedirectToAction("Index", "Events"),
                "Details" => RedirectToAction("Details", "Events", new { id = returnId ?? eventId }),
                _         => RedirectToAction("Index")
            };
        }

        [HttpPost]
        public IActionResult Remove(int outcomeId, string? ret = null)
        {
            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
            var item = items.FirstOrDefault(x => x.OutcomeId == outcomeId);

            if (item != null)
            {
                items.Remove(item);
                HttpContext.Session.SetObject(BetSlipSessionKey, items);
                // OutcomeRemoved nu se loghează pe Ethical — eliminarea individuală a selecțiilor
                // nu este o acțiune distinctivă în varianta etică (utilizatorul nu depășește
                // un pre-filled betslip; absența logului este intenționată).
            }

            return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Clear(string? ret = null)
        {
            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Ethical",
                    "BetSlipCleared", "BetSlip", "Button", "clear-betslip-button",
                    null, null, "{}");
            }

            HttpContext.Session.Remove(BetSlipSessionKey);
            return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Place(decimal stake, string? ret = null)
        {
            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
            if (!items.Any())
            {
                TempData["Error"] = "Biletul este gol.";
                return RedirectToAction("Index");
            }

            var userIdString = HttpContext.Session.GetString("Ethical_CurrentUserId");
            if (!Guid.TryParse(userIdString, out var userId))
            {
                TempData["Error"] = "Sesiunea a expirat. Autentifică-te din nou.";
                return RedirectToAction("Index");
            }

            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId, "Ethical",
                    "StakeChanged", "BetSlip", "StakeInput", "main-betslip",
                    null, stake.ToString("F2"), $"{{\"currency\":\"RON\"}}");

                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId, "Ethical",
                    "BetAttempted", "BetSlip", "Button", "place-bet-button",
                    null, null,
                    $"{{\"stake\":\"{stake}\",\"selectionCount\":\"{items.Count}\"}}");
            }

            var (success, error) = await _apiService.PlaceBetAsync(
                userId,
                stake,
                items.Select(x => x.OutcomeId).ToList());

            if (!success)
            {
                TempData["Error"] = error ?? "Eroare la plasarea pariului. Verifică soldul și încearcă din nou.";
                if (experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(
                        experimentSessionId.Value, userId, "Ethical",
                        "BetPlacedFailed", "BetSlip", "Button", "place-bet-button",
                        null, null,
                        $"{{\"stake\":\"{stake}\",\"error\":\"{error?.Replace("\"", "'")}\"}}");
                }
                return RedirectToAction("Index");
            }

            TempData["Success"] = "Pariul a fost plasat cu succes!";
            HttpContext.Session.Remove(BetSlipSessionKey);
            if (experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId, "Ethical",
                    "BetPlaced", "BetSlip", "Button", "place-bet-button",
                    null, null,
                    $"{{\"stake\":\"{stake}\",\"selectionCount\":\"{items.Count}\"}}");
            }

            return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
        }

        private static decimal CalculateTotalOdds(List<BetSlipItemViewModel> items)
        {
            if (!items.Any())
                return 0;

            decimal total = 1m;
            foreach (var item in items)
            {
                total *= item.Odds;
            }

            return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}