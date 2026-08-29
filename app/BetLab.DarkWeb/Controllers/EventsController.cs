using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public EventsController(ApiService apiService, DarkSessionManager sessionManager)
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
                .Cast<BetLab.DarkWeb.Models.EventDetailsViewModel>()
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Dark",
                    "PageViewed", "Events", "Page", "EventsIndex",
                    null, null, $"{{\"source\":\"navigation\",\"page\":{page}}}");
            }

            return View(details);
        }

        public async Task<IActionResult> Details(int id)
        {
            // Fetch în paralel pentru viteză
            var eventTask       = _apiService.GetEventByIdAsync(id);
            var analyticsTask   = _apiService.GetEventAnalyticsAsync(id);
            var popularityTask  = _apiService.GetEventPopularityAsync(id);
            await Task.WhenAll(eventTask, analyticsTask, popularityTask);

            var eventDetails = await eventTask;
            if (eventDetails == null)
                return NotFound();

            var pageModel = new BetLab.DarkWeb.Models.EventDetailsPageViewModel
            {
                Event      = eventDetails,
                Analytics  = await analyticsTask,
                Popularity = await popularityTask
            };

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(
                    experimentSessionId.Value, userId.Value, "Dark",
                    "PageViewed", "EventDetails", "Event", id.ToString(),
                    null, null,
                    $"{{\"eventName\":\"{eventDetails.HomeTeam} vs {eventDetails.AwayTeam}\"}}");
            }

            return View(pageModel);
        }
    }
}
