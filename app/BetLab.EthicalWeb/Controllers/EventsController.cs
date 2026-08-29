using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public EventsController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        private const int PageSize = 6;

        public async Task<IActionResult> Index(int page = 1)
        {
            var events = await _apiService.GetEventsAsync();

            var totalCount = events.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            page = Math.Clamp(page, 1, Math.Max(1, totalPages));

            var pageEvents = events.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            var detailTasks = pageEvents.Select(e => _apiService.GetEventByIdAsync(e.EventId));
            var details = (await Task.WhenAll(detailTasks))
                .Where(d => d != null)
                .Cast<BetLab.EthicalWeb.Models.EventDetailsViewModel>()
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value,
                    userId.Value,
                    "Ethical",
                    "PageViewed",
                    "Events",
                    "Page",
                    "EventsIndex",
                    null,
                    null,
                    "{\"source\":\"navigation\"}");
            }

            return View(details);
        }

        public async Task<IActionResult> Details(int id)
        {
            var eventDetails = await _apiService.GetEventByIdAsync(id);
            if (eventDetails == null)
                return NotFound();

            // Best-effort: aducem și statistici (H2H + formă) ca să informăm utilizatorul
            var analytics = await _apiService.GetEventAnalyticsAsync(id);

            var model = new BetLab.EthicalWeb.Models.EventDetailsPageViewModel
            {
                Event     = eventDetails,
                Analytics = analytics
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
                    "EventDetails",
                    "Event",
                    id.ToString(),
                    null,
                    null,
                    $"{{\"eventName\":\"{eventDetails.HomeTeam} vs {eventDetails.AwayTeam}\"}}");
            }

            return View(model);
        }
    }
}
