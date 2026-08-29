using System.Text;
using System.Text.Json;
using BetLab.EthicalWeb.Models;

namespace BetLab.EthicalWeb.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<EventListItemViewModel>> GetEventsAsync()
        {
            var response = await _httpClient.GetAsync("api/events");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<EventListItemViewModel>>(json, _jsonOptions) ?? new();
        }

        public async Task<EventDetailsViewModel?> GetEventByIdAsync(int id)
        {
            var response = await _httpClient.GetAsync($"api/events/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventDetailsViewModel>(json, _jsonOptions);
        }

        
        /// Aduce statistici detaliate (H2H, formă, agregate) pentru un meci.
        /// Best-effort — întoarce null dacă API-ul de analytics nu răspunde.
        
        public async Task<EventAnalyticsViewModel?> GetEventAnalyticsAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/events/{id}/analytics");
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<EventAnalyticsViewModel>(json, _jsonOptions);
            }
            catch { return null; }
        }

        // Cash Out

        public async Task<CashOutPreviewViewModel?> GetCashOutPreviewAsync(string token, int betSlipId, Guid userId)
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get, $"api/bets/{betSlipId}/cashout-preview?userId={userId}&variant=Ethical");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CashOutPreviewViewModel>(json, _jsonOptions);
        }

        public async Task<(bool Success, decimal? CashoutValue, decimal? NewBalance, string? Error)> CashOutAsync(
            string token, int betSlipId, Guid userId)
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"api/bets/{betSlipId}/cashout?userId={userId}&variant=Ethical");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    var err = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                    return (false, null, null, err ?? "Eroare la cash out.");
                }
                catch { return (false, null, null, "Eroare la cash out."); }
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cashoutValue = root.TryGetProperty("cashoutValue", out var cv) ? (decimal?)cv.GetDecimal() : null;
            var newBalance   = root.TryGetProperty("newBalance",   out var nb) ? (decimal?)nb.GetDecimal() : null;
            return (true, cashoutValue, newBalance, null);
        }

        public async Task<bool> StartSessionAsync(Guid userId, string variant)
        {
            var payload = new
            {
                userId,
                variant,
                deviceType = "Desktop",
                browser = "Browser"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/logging/session/start", content);
            return response.IsSuccessStatusCode;
        }
        public async Task<bool> LogEventAsync(
    Guid sessionId,
    Guid userId,
    string variant,
    string eventType,
    string pageName,
    string? targetType,
    string? targetId,
    string? oldValue,
    string? newValue,
    string? metadataJson)
        {
            var payload = new
            {
                sessionId,
                userId,
                variant,
                eventType,
                pageName,
                targetType,
                targetId,
                oldValue,
                newValue,
                metadataJson
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/logging/event", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<string?> StartSessionAndGetIdAsync(Guid userId, string variant)
        {
            var payload = new
            {
                userId,
                variant,
                deviceType = "Desktop",
                browser = "Browser"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/logging/session/start", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement.GetProperty("sessionId").GetString();
        }

        public async Task<(bool Success, string? Error)> PlaceBetAsync(Guid userId, decimal stake, List<int> outcomeIds)
        {
            var payload = new
            {
                userId,
                stake,
                selections = outcomeIds.Select(id => new { outcomeId = id }).ToList()
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/bets/place", content);
            if (response.IsSuccessStatusCode)
                return (true, null);

            var error = await response.Content.ReadAsStringAsync();
            return (false, error?.Trim('"') ?? "Eroare la plasarea pariului.");
        }
        public async Task<bool> EndSessionAsync(Guid sessionId, Guid userId)
        {
            var payload = new
            {
                sessionId,
                userId
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/logging/session/end", content);
            return response.IsSuccessStatusCode;
        }
        public async Task<List<CasinoGameViewModel>> GetCasinoGamesAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/casino/games");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<CasinoGameViewModel>>(json, _jsonOptions) ?? new();
        }

        public async Task<(CasinoSpinResponseViewModel? Result, string? Error)> SpinCasinoAsync(string token, int casinoGameId, decimal stake)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/casino/spin");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                casinoGameId,
                stake,
                casinoSessionId = (Guid?)null
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var errorMsg = errorBody.Trim('"', ' ');
                return (null, string.IsNullOrWhiteSpace(errorMsg) ? "Rotirea a eșuat." : errorMsg);
            }

            var json = await response.Content.ReadAsStringAsync();
            return (JsonSerializer.Deserialize<CasinoSpinResponseViewModel>(json, _jsonOptions), null);
        }

        public async Task<List<CasinoRoundHistoryViewModel>> GetCasinoHistoryAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/casino/history/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<CasinoRoundHistoryViewModel>>(json, _jsonOptions) ?? new();
        }
        public async Task<AuthResponseViewModel?> LoginAsync(string email, string password)
        {
            var payload = new
            {
                email,
                password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("api/auth/login", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AuthResponseViewModel>(json, _jsonOptions);
        }

        public async Task<(AuthResponseViewModel? Result, string? Error)> RegisterAsync(
            string email, string userName, string password, string cnp)
        {
            var payload = new { email, userName, password, cnp };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("api/auth/register", content);
            var json     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Extragem mesajul de eroare din API
                var errMsg = json?.Trim('"') ?? "Eroare necunoscută.";
                return (null, errMsg);
            }

            var result = JsonSerializer.Deserialize<AuthResponseViewModel>(json, _jsonOptions);
            return (result, null);
        }

        public async Task<CurrentUserViewModel?> GetCurrentUserAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CurrentUserViewModel>(json, _jsonOptions);
        }

        public async Task<WalletDtoViewModel?> GetMyWalletAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/wallet/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WalletDtoViewModel>(json, _jsonOptions);
        }

        public async Task<List<BetHistoryItemViewModel>> GetMyBetsAsync(string token, Guid userId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/bets/user/{userId}");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<BetHistoryItemViewModel>();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<BetHistoryItemViewModel>>(json, _jsonOptions) ?? new();
        }

        public async Task<List<WalletTransactionViewModel>> GetMyWalletTransactionsAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/wallet/transactions");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<WalletTransactionViewModel>();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<WalletTransactionViewModel>>(json, _jsonOptions) ?? new();
        }
        
     
        public async Task<UserLimitViewModel?> GetMyLimitsAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/userlimits/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserLimitViewModel>(json, _jsonOptions);
        }

        public async Task<(bool Success, UserLimitViewModel? Limits, string? Error)> SetMyLimitsAsync(
            string token,
            decimal? dailyDepositLimit,
            decimal? dailyLossLimit,
            int? dailySessionMinutesLimit,
            int? realityCheckIntervalMinutes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/userlimits/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                dailyDepositLimit,
                dailyLossLimit,
                dailySessionMinutesLimit,
                realityCheckIntervalMinutes
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // API-ul returnează BadRequest cu un string simplu (mesaj prietenos).
                
                var errorMessage = json?.Trim('"');
                return (false, null, errorMessage ?? "Eroare la salvarea limitelor.");
            }

            var limits = JsonSerializer.Deserialize<UserLimitViewModel>(json, _jsonOptions);
            return (true, limits, null);
        }

        public async Task<(bool Success, UserLimitViewModel? Limits, string? Error)> SelfExcludeAsync(
            string token, string duration)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/userlimits/me/self-exclude");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new { duration };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, json?.Trim('"') ?? "Eroare la auto-excludere.");

            var limits = JsonSerializer.Deserialize<UserLimitViewModel>(json, _jsonOptions);
            return (true, limits, null);
        }

        public async Task<(bool Success, UserLimitViewModel? Limits, string? Error)> CancelSelfExcludeAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/userlimits/me/cancel-self-exclude");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, json?.Trim('"') ?? "Eroare la anularea auto-excluderii.");

            var limits = JsonSerializer.Deserialize<UserLimitViewModel>(json, _jsonOptions);
            return (true, limits, null);
        }

        public async Task<(bool Success, decimal? NewBalance, string? Error)> DepositAsync(string token, decimal amount)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/wallet/topup-demo");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new { amount };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, null, err?.Trim('"') ?? "Eroare la depunere.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var wallet = JsonSerializer.Deserialize<WalletDtoViewModel>(json, _jsonOptions);
            return (true, wallet?.Balance, null);
        }

        public async Task<RouletteSpinResponseViewModel?> RouletteSpinAsync(
            string token, string betType, string betValue, decimal stake)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/roulette/spin");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new { betType, betValue, stake };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RouletteSpinResponseViewModel>(json, _jsonOptions);
        }

        public async Task<List<RouletteHistoryViewModel>> GetRouletteHistoryAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/roulette/history/me");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RouletteHistoryViewModel>>(json, _jsonOptions) ?? new();
        }

        public async Task<BlackjackStateViewModel?> BlackjackStartAsync(string token, decimal stake)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/blackjack/start");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(new { stake }), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<BlackjackStateViewModel>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
        }

        public async Task<BlackjackStateViewModel?> BlackjackHitAsync(string token, long sessionId)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/blackjack/hit");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(new { sessionId }), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<BlackjackStateViewModel>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
        }

        public async Task<BlackjackStateViewModel?> BlackjackStandAsync(string token, long sessionId)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/blackjack/stand");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(new { sessionId }), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<BlackjackStateViewModel>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
        }

        

        private HttpRequestMessage AdminRequest(HttpMethod method, string url, string token)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return req;
        }

        public async Task<AdminDashboardViewModel?> AdminGetDashboardAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/dashboard", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<AdminDashboardViewModel>(await resp.Content.ReadAsStringAsync(), _jsonOptions);
        }

        public async Task<List<AdminUserViewModel>> AdminGetUsersAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/users", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminUserViewModel>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<bool> AdminResetBalanceAsync(string token, Guid userId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/users/{userId}/reset-balance", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminBlockUserAsync(string token, Guid userId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/users/{userId}/block", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminUnblockUserAsync(string token, Guid userId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/users/{userId}/unblock", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<List<AdminEventViewModel>> AdminGetEventsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/events", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminEventViewModel>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<bool> AdminMarkWinningOutcomeAsync(string token, int outcomeId)
        {
            using var req = AdminRequest(HttpMethod.Post, "api/admin/settlement/mark-winning-outcome", token);
            req.Content = new StringContent(JsonSerializer.Serialize(new { outcomeId }), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminSettleEventAsync(string token, int eventId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/settlement/event/{eventId}", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminMarkEventLiveAsync(string token, int eventId, bool isLive)
        {
            using var req = AdminRequest(HttpMethod.Put, $"api/admin/events/{eventId}/status", token);
            var payload = new { status = isLive ? "Live" : "Open", isLive };
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminCreateEventAsync(string token, int sportId, int competitionId,
            int homeTeamId, int awayTeamId, DateTime startTimeUtc,
            string homeTeamName, string awayTeamName,
            decimal oddsHome, decimal oddsDraw, decimal oddsAway)
        {
            using var req1 = AdminRequest(HttpMethod.Post, "api/admin/events", token);
            var eventPayload = new { sportId, competitionId, homeTeamId, awayTeamId, startTimeUtc, status = "Open", isLive = false };
            req1.Content = new StringContent(JsonSerializer.Serialize(eventPayload), Encoding.UTF8, "application/json");
            var resp1 = await _httpClient.SendAsync(req1);
            if (!resp1.IsSuccessStatusCode) return false;

            var json1 = await resp1.Content.ReadAsStringAsync();
            using var doc1 = JsonDocument.Parse(json1);
            var eventId = doc1.RootElement.GetProperty("eventId").GetInt32();

            using var req2 = AdminRequest(HttpMethod.Post, "api/admin/markets", token);
            var marketPayload = new { eventId, name = "Full Time Result", marketType = "1X2", status = "Open", isMain = true };
            req2.Content = new StringContent(JsonSerializer.Serialize(marketPayload), Encoding.UTF8, "application/json");
            var resp2 = await _httpClient.SendAsync(req2);
            if (!resp2.IsSuccessStatusCode) return false;

            var json2 = await resp2.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(json2);
            var marketId = doc2.RootElement.GetProperty("marketId").GetInt32();

            var outcomes = new[]
            {
                new { marketId, name = homeTeamName, code = "1", odds = oddsHome, status = "Open" },
                new { marketId, name = "Egalitate",   code = "X", odds = oddsDraw, status = "Open" },
                new { marketId, name = awayTeamName,  code = "2", odds = oddsAway, status = "Open" }
            };

            foreach (var o in outcomes)
            {
                using var req3 = AdminRequest(HttpMethod.Post, "api/admin/outcomes", token);
                req3.Content = new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
                var resp3 = await _httpClient.SendAsync(req3);
                if (!resp3.IsSuccessStatusCode) return false;
            }
            return true;
        }

        public async Task<List<AdminLookupItem>> AdminGetSportsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/lookups/sports", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminLookupItem>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<List<AdminLookupItem>> AdminGetCompetitionsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/lookups/competitions", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminLookupItem>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<List<AdminLookupItem>> AdminGetTeamsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/lookups/teams", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminLookupItem>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<List<AdminCasinoGameViewModel>> AdminGetCasinoGamesAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/casino-games", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminCasinoGameViewModel>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<bool> AdminToggleCasinoGameAsync(string token, int gameId)
        {
            using var req = AdminRequest(HttpMethod.Put, $"api/admin/casino-games/{gameId}/toggle", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<List<AdminExclusionViewModel>> AdminGetExclusionsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/self-exclusion", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();
            return JsonSerializer.Deserialize<List<AdminExclusionViewModel>>(await resp.Content.ReadAsStringAsync(), _jsonOptions) ?? new();
        }

        public async Task<bool> AdminImposeExclusionAsync(string token, Guid userId, string reason, bool permanent, DateTime? excludedUntil)
        {
            using var req = AdminRequest(HttpMethod.Post, "api/admin/self-exclusion/impose", token);
            var payload = new { userId, reason, permanentExclusion = permanent, excludedUntil };
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> AdminLiftExclusionAsync(string token, long exclusionId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/self-exclusion/{exclusionId}/lift", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<List<CnpExclusionViewModel>> AdminGetCnpExclusionsAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Get, "api/admin/cnp-exclusions", token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var list = new List<CnpExclusionViewModel>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new CnpExclusionViewModel
                {
                    Id         = el.TryGetProperty("id",         out var id)  ? id.GetInt64()         : 0,
                    CnpMasked  = el.TryGetProperty("cnp",        out var cnp) ? cnp.GetString() ?? "" : "",
                    UserName   = el.TryGetProperty("userName",   out var un)  ? un.GetString()        : null,
                    ExcludedAt = el.TryGetProperty("excludedAt", out var ea)  ? ea.GetDateTime()      : DateTime.UtcNow,
                    Reason     = el.TryGetProperty("reason",     out var r)   ? r.GetString()         : null
                });
            }
            return list;
        }

        public async Task<bool> AdminRemoveCnpExclusionAsync(string token, long cnpId)
        {
            using var req = AdminRequest(HttpMethod.Delete, $"api/admin/cnp-exclusions/{cnpId}", token);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        public async Task<(bool Ok, string Message)> AdminSyncEventsAsync(string token, bool autoSettle)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/sync-events?autoSettle={autoSettle}", token);
            var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return (false, "Eroare la sincronizare.");
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (true, msg);
        }

        public async Task<(bool Ok, string Message)> AdminImportApiFootballAsync(
            string token, int leagueId, int season, string name, string country = "Romania",
            string? dateFrom = null, string? dateTo = null, bool onlyUpcoming = true)
        {
            var qs = $"leagueId={leagueId}&season={season}&name={Uri.EscapeDataString(name)}&country={Uri.EscapeDataString(country)}&onlyUpcoming={onlyUpcoming.ToString().ToLowerInvariant()}";
            if (!string.IsNullOrWhiteSpace(dateFrom)) qs += $"&dateFrom={Uri.EscapeDataString(dateFrom)}";
            if (!string.IsNullOrWhiteSpace(dateTo))   qs += $"&dateTo={Uri.EscapeDataString(dateTo)}";

            using var req = AdminRequest(HttpMethod.Post, $"api/admin/import-apifootball?{qs}", token);
            var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                using var errDoc = JsonDocument.Parse(json);
                var err = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "Eroare la import.";
                return (false, err);
            }
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (true, msg);
        }

        public async Task<(bool Ok, string Message)> AdminSyncLiveApiFootballAsync(string token)
        {
            using var req = AdminRequest(HttpMethod.Post, "api/admin/sync-live-apifootball", token);
            var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                using var errDoc = JsonDocument.Parse(json);
                var err = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "Eroare la sync live.";
                return (false, err);
            }
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (true, msg);
        }

        public async Task<(bool Ok, string Message)> AdminImportApiFootballFixtureAsync(string token, int fixtureId)
        {
            using var req = AdminRequest(HttpMethod.Post, $"api/admin/import-apifootball-fixture/{fixtureId}", token);
            var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                using var errDoc = JsonDocument.Parse(json);
                var err = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "Eroare la import.";
                return (false, err);
            }
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (true, msg);
        }

        public async Task<AdminTransactionsPageViewModel?> AdminGetTransactionsAsync(string token, string? type, int page)
        {
            var url = $"api/admin/transactions?page={page}&pageSize=50{(string.IsNullOrWhiteSpace(type) ? "" : $"&type={type}")}";
            using var req = AdminRequest(HttpMethod.Get, url, token);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new AdminTransactionsPageViewModel
            {
                Total    = root.GetProperty("total").GetInt32(),
                Page     = root.GetProperty("page").GetInt32(),
                PageSize = root.GetProperty("pageSize").GetInt32(),
                Filter   = type,
                Items    = JsonSerializer.Deserialize<List<AdminTransactionViewModel>>(
                               root.GetProperty("items").GetRawText(), _jsonOptions) ?? new()
            };
        }

    }
}