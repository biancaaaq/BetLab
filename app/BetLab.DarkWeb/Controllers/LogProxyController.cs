using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    [Route("logging")]
    [IgnoreAntiforgeryToken]
    public class LogProxyController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public LogProxyController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        [HttpPost("proxy")]
        public async Task<IActionResult> Proxy([FromBody] LogProxyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EventType) || string.IsNullOrWhiteSpace(request.PageName))
                return BadRequest();

            var userId = _sessionManager.GetCurrentUserId();
            var sessionId = await _sessionManager.EnsureExperimentSessionAsync();

            if (!userId.HasValue || !sessionId.HasValue)
                return Ok();

            await _apiService.LogEventAsync(
                sessionId.Value, userId.Value, "Dark",
                request.EventType, request.PageName,
                request.TargetType, request.TargetId,
                request.OldValue, request.NewValue, request.MetadataJson);

            return Ok();
        }
    }

    public class LogProxyRequest
    {
        public string EventType { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? MetadataJson { get; set; }
    }
}
