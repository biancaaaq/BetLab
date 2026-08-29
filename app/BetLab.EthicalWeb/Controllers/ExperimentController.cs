using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class ExperimentController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public ExperimentController(ApiService apiService, EthicalSessionManager sessionManager)
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