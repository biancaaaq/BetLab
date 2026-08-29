namespace BetLab.EthicalWeb.Services
{
    public class EthicalSessionManager
    {
        private const string ExperimentSessionKey = "Ethical_ExperimentSessionId";
        private readonly ApiService _apiService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EthicalSessionManager(ApiService apiService, IHttpContextAccessor httpContextAccessor)
        {
            _apiService = apiService;
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? GetCurrentUserId()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var userId = session?.GetString("Ethical_CurrentUserId");
            return Guid.TryParse(userId, out var parsed) ? parsed : null;
        }

        public async Task<Guid?> EnsureExperimentSessionAsync()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return null;

            var existingSessionId = session.GetString(ExperimentSessionKey);
            if (!string.IsNullOrWhiteSpace(existingSessionId) && Guid.TryParse(existingSessionId, out var parsedSessionId))
                return parsedSessionId;

            var userId = GetCurrentUserId();
            if (userId == null)
                return null;

            var sessionIdString = await _apiService.StartSessionAndGetIdAsync(userId.Value, "Ethical");
            if (string.IsNullOrWhiteSpace(sessionIdString))
                return null;

            session.SetString(ExperimentSessionKey, sessionIdString);
            return Guid.Parse(sessionIdString);
        }

        public Guid? GetCurrentExperimentSessionId()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var sessionId = session?.GetString(ExperimentSessionKey);
            return Guid.TryParse(sessionId, out var parsed) ? parsed : null;
        }

        public void ClearExperimentSession()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            session?.Remove(ExperimentSessionKey);
        }
    }
}
