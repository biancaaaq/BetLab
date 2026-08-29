using BetLab.DarkWeb.Helpers;
using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class BetSlipController : Controller
    {
        private const string BetSlipSessionKey = "Dark_BetSlip";
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public BetSlipController(ApiService apiService, DarkSessionManager sessionManager)
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
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                    "PageViewed", "BetSlip", "Page", "BetSlipIndex", null, null,
                    $"{{\"itemCount\":\"{items.Count}\"}}");
            }

            return View(model);
        }

        public async Task<IActionResult> AddOutcome(int outcomeId)
        {
            // Dark pattern: neautentificat → împinge spre Register (conversie nouă), nu Login.
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Dark_JwtToken")))
            {
                TempData["DarkRegisterNudge"] = "Creează cont gratuit ca să plasezi pariul!";
                return RedirectToAction("Register", "Account");
            }

            var events = await _apiService.GetEventsAsync();
            foreach (var ev in events)
            {
                var details = await _apiService.GetEventByIdAsync(ev.EventId);
                if (details == null) continue;

                foreach (var market in details.Markets)
                {
                    var outcome = market.Outcomes.FirstOrDefault(o => o.OutcomeId == outcomeId);
                    if (outcome != null)
                    {
                        var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
                        var alreadyThis = items.Any(x => x.OutcomeId == outcomeId);

                        if (!alreadyThis)
                        {
                            var newItem = new BetSlipItemViewModel
                            {
                                OutcomeId   = outcomeId,
                                EventId     = details.EventId,
                                EventName   = $"{details.HomeTeam} vs {details.AwayTeam}",
                                MarketId    = market.MarketId,
                                MarketName  = market.Name,
                                OutcomeName = outcome.Name,
                                Odds        = outcome.Odds,
                                MarketType  = market.MarketType,
                                OutcomeCode = outcome.Code
                            };

                            // Verificăm conflicte față de selecțiile din același eveniment,
                            // excluzând eventualul slot al aceleiași piețe (care va fi înlocuit)
                            var otherItems = items
                                .Where(x => !(x.EventId == details.EventId && x.MarketId == market.MarketId))
                                .ToList();

                            var conflict = BetSlipConflictChecker.GetConflict(newItem, otherItems);
                            if (conflict != null)
                            {
                                TempData["Error"] = $"Selecție blocată: {conflict}";
                                HttpContext.Session.SetObject(BetSlipSessionKey, items);
                                return RedirectToAction("Index", "Events");
                            }

                            // Înlocuim orice selecție anterioară din aceeași piață
                            items.RemoveAll(x => x.EventId == details.EventId && x.MarketId == market.MarketId);
                            items.Add(newItem);
                        }
                        else
                        {
                            // Toggle off — deselect
                            items.RemoveAll(x => x.EventId == details.EventId && x.MarketId == market.MarketId);
                        }

                        HttpContext.Session.SetObject(BetSlipSessionKey, items);
                        return RedirectToAction("Index", "Events");
                    }
                }
            }
            return RedirectToAction("Index", "Events");
        }

        [HttpPost]
        public async Task<IActionResult> Add(
            int outcomeId, int eventId, string eventName,
            int marketId, string marketName, string outcomeName, decimal odds,
            string marketType = "", string outcomeCode = "",
            string? ret = null, int? returnId = null)
        {
            // Dark pattern: neautentificat → împinge spre Register (conversie nouă), nu Login.
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Dark_JwtToken")))
            {
                TempData["DarkRegisterNudge"] = $"Doar 30 de secunde — creează cont ca să pariezi pe «{eventName}»!";
                return RedirectToAction("Register", "Account");
            }

            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();

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

                // Verificăm conflicte excluzând slotul aceleiași piețe (va fi înlocuit)
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

                // Înlocuim selecția anterioară din aceeași piață (dacă există) și adăugăm
                if (existingForMarket != null) items.Remove(existingForMarket);
                items.Add(newItem);

                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                        "OutcomeSelected", "EventDetails", "Outcome", outcomeId.ToString(),
                        null, outcomeName,
                        $"{{\"eventName\":\"{eventName}\",\"marketName\":\"{marketName}\",\"odds\":\"{odds}\"}}");
                }
            }
            else
            {
                // Toggle off — deselect aceeași cotă
                if (existingForMarket != null) items.Remove(existingForMarket);
            }

            HttpContext.Session.SetObject(BetSlipSessionKey, items);

            return ret switch
            {
                "Events"  => RedirectToAction("Index", "Events"),
                "Details" => RedirectToAction("Details", "Events", new { id = returnId ?? eventId }),
                _         => RedirectToAction("Index")
            };
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int outcomeId, string? ret = null)
        {
            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
            var item = items.FirstOrDefault(x => x.OutcomeId == outcomeId);

            if (item != null)
            {
                items.Remove(item);
                HttpContext.Session.SetObject(BetSlipSessionKey, items);

                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                        "OutcomeRemoved", "BetSlip", "Outcome", outcomeId.ToString(),
                        item.OutcomeName, null, $"{{\"eventName\":\"{item.EventName}\"}}");
                }
            }

            return ret == "Events"
                ? RedirectToAction("Index", "Events")
                : RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Clear(string? ret = null)
        {
            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                    "BetSlipCleared", "BetSlip", "Button", "clear-betslip-button",
                    null, null, "{}");
            }

            HttpContext.Session.Remove(BetSlipSessionKey);
            return ret == "Events"
                ? RedirectToAction("Index", "Events")
                : RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Place(decimal stake, string? ret = null)
        {
            var items = HttpContext.Session.GetObject<List<BetSlipItemViewModel>>(BetSlipSessionKey) ?? new();
            if (!items.Any())
            {
                TempData["Error"] = "Biletul este gol.";
                return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
            }

            var userIdString = HttpContext.Session.GetString("Dark_CurrentUserId");
            if (!Guid.TryParse(userIdString, out var userId))
            {
                TempData["Error"] = "Sesiunea a expirat.";
                return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
            }

            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId, "Dark",
                    "StakeChanged", "BetSlip", "StakeInput", "main-betslip",
                    null, stake.ToString("F2"), $"{{\"currency\":\"RON\"}}");

                await _apiService.LogEventAsync(experimentSessionId.Value, userId, "Dark",
                    "BetAttempted", "BetSlip", "Button", "place-bet-button", null, null,
                    $"{{\"stake\":\"{stake}\",\"selectionCount\":\"{items.Count}\"}}");
            }

            var (success, error) = await _apiService.PlaceBetAsync(
                userId, stake, items.Select(x => x.OutcomeId).ToList());

            if (success)
            {
                HttpContext.Session.Remove(BetSlipSessionKey);
                TempData["Success"] = "Pariu plasat cu succes!";
                if (experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId, "Dark",
                        "BetPlaced", "BetSlip", "Button", "place-bet-button", null, null,
                        $"{{\"stake\":\"{stake}\",\"selectionCount\":\"{items.Count}\"}}");
                }
            }
            else
            {
                TempData["Error"] = error ?? "Eroare la plasarea pariului. Verifică soldul.";
                if (experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId, "Dark",
                        "BetPlacedFailed", "BetSlip", "Button", "place-bet-button", null, null,
                        $"{{\"stake\":\"{stake}\",\"error\":\"{error?.Replace("\"", "'")}\"}}");
                }
            }

            return ret == "Events" ? RedirectToAction("Index", "Events") : RedirectToAction("Index");
        }

        private static decimal CalculateTotalOdds(List<BetSlipItemViewModel> items)
        {
            if (!items.Any()) return 0;
            decimal total = 1m;
            foreach (var item in items) total *= item.Odds;
            return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}
