using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class ExperimentController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public ExperimentController(ApiService apiService, DarkSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        [HttpPost]
        public async Task<IActionResult> EndSession()
        {
            var userId = _sessionManager.GetCurrentUserId();
            var sessionId = _sessionManager.GetCurrentExperimentSessionId();

            if (userId.HasValue && sessionId.HasValue)
            {
                await _apiService.EndSessionAsync(sessionId.Value, userId.Value);
                _sessionManager.ClearExperimentSession();
            }

            return RedirectToAction("Index", "Home");
        }
    }
}