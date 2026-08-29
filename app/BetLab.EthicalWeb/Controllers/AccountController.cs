using BetLab.EthicalWeb.Models;
using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiService _apiService;
        private readonly EthicalSessionManager _sessionManager;

        public AccountController(ApiService apiService, EthicalSessionManager sessionManager)
        {
            _apiService = apiService;
            _sessionManager = sessionManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                ViewBag.Error = "Email și parola sunt obligatorii.";
                return View(model);
            }

            var result = await _apiService.LoginAsync(model.Email, model.Password);
            if (result == null)
            {
                ViewBag.Error = "Autentificare eșuată. Verifică emailul și parola.";
                return View(model);
            }

            HttpContext.Session.SetString("Ethical_JwtToken", result.Token);
            HttpContext.Session.SetString("Ethical_CurrentEmail", result.Email);
            HttpContext.Session.SetString("Ethical_CurrentUserName", result.UserName);
            HttpContext.Session.SetString("Ethical_CurrentUserId", result.UserId.ToString());

            if (result.Roles.Contains("Admin"))
                HttpContext.Session.SetString("Ethical_IsAdmin", "true");

            var loginUserId = _sessionManager.GetCurrentUserId();
            var loginSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (loginUserId.HasValue && loginSessionId.HasValue)
            {
                await _apiService.LogEventAsync(loginSessionId.Value, loginUserId.Value, "Ethical",
                    "LoginSuccess", "Login", "Form", "login-form",
                    null, null, "{}");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.UserName) ||
                string.IsNullOrWhiteSpace(model.Password) ||
                string.IsNullOrWhiteSpace(model.Cnp))
            {
                ViewBag.Error = "Toate câmpurile sunt obligatorii, inclusiv CNP-ul.";
                return View(model);
            }

            var (result, error) = await _apiService.RegisterAsync(
                model.Email, model.UserName, model.Password, model.Cnp);

            if (result == null)
            {
                // Ethical: mesaj clar și specific 
                ViewBag.Error = error == "CNP_EXCLUDED"
                    ? "Acest CNP se află în Registrul de Autoexcludere (ONJN). Nu poți crea un cont nou. " +
                      "Dacă crezi că este o eroare, contactează-ne la support@betlab.ro."
                    : error ?? "Crearea contului a eșuat.";
                return View(model);
            }

            HttpContext.Session.SetString("Ethical_JwtToken", result.Token);
            HttpContext.Session.SetString("Ethical_CurrentEmail", result.Email);
            HttpContext.Session.SetString("Ethical_CurrentUserName", result.UserName);
            HttpContext.Session.SetString("Ethical_CurrentUserId", result.UserId.ToString());

            if (result.Roles.Contains("Admin"))
                HttpContext.Session.SetString("Ethical_IsAdmin", "true");

            var regUserId = _sessionManager.GetCurrentUserId();
            var regSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (regUserId.HasValue && regSessionId.HasValue)
            {
                await _apiService.LogEventAsync(regSessionId.Value, regUserId.Value, "Ethical",
                    "RegisterSuccess", "Register", "Form", "register-form",
                    null, null, "{}");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = _sessionManager.GetCurrentExperimentSessionId();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                    "Logout", "Home", "Button", "logout-button",
                    null, null, "{}");
                // Închidem sesiunea de experiment în DB (setează EndedAt)
                await _apiService.EndSessionAsync(experimentSessionId.Value, userId.Value);
            }

            _sessionManager.ClearExperimentSession();
            HttpContext.Session.Remove("Ethical_JwtToken");
            HttpContext.Session.Remove("Ethical_CurrentEmail");
            HttpContext.Session.Remove("Ethical_CurrentUserName");
            HttpContext.Session.Remove("Ethical_CurrentUserId");
            HttpContext.Session.Remove("Ethical_IsAdmin");

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Deposit()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            // Calculăm depunerile de azi pentru a afișa progresul față de limită
            var limits       = await _apiService.GetMyLimitsAsync(token);
            var transactions = await _apiService.GetMyWalletTransactionsAsync(token);
            var today        = DateTime.UtcNow.Date;
            var todayDeposited = transactions
                .Where(t => (t.Type == "TopUp" || t.Type == "DemoTopUp") && t.CreatedAt >= today)
                .Sum(t => t.Amount);

            ViewBag.DailyDepositLimit  = limits?.DailyDepositLimit;
            ViewBag.TodayDeposited     = todayDeposited;

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                    "DepositStarted", "Deposit", "Page", "DepositIndex",
                    null, null, "{}");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Deposit(decimal amount)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            if (amount <= 0)
            {
                TempData["DepositError"] = "Suma trebuie să fie mai mare de 0.";
                return RedirectToAction("Deposit");
            }

            // Verificăm limita zilnică de depunere înainte de a procesa
            var limits = await _apiService.GetMyLimitsAsync(token);
            if (limits?.DailyDepositLimit.HasValue == true)
            {
                var transactions = await _apiService.GetMyWalletTransactionsAsync(token);
                var today        = DateTime.UtcNow.Date;
                var todayDeposited = transactions
                    .Where(t => (t.Type == "TopUp" || t.Type == "DemoTopUp") && t.CreatedAt >= today)
                    .Sum(t => t.Amount);

                if (todayDeposited + amount > limits.DailyDepositLimit.Value)
                {
                    var remaining = Math.Max(0, limits.DailyDepositLimit.Value - todayDeposited);
                    TempData["DepositError"] = remaining > 0
                        ? $"Limita zilnică de depunere este {limits.DailyDepositLimit.Value:F0} RON. " +
                          $"Ai mai depus {todayDeposited:F0} RON azi, poți adăuga maximum {remaining:F0} RON."
                        : $"Ai atins limita zilnică de depunere de {limits.DailyDepositLimit.Value:F0} RON. " +
                           "Limita se resetează la miezul nopții.";
                    return RedirectToAction("Deposit");
                }
            }

            var (success, newBalance, error) = await _apiService.DepositAsync(token, amount);
            if (success)
            {
                TempData["DepositSuccess"] = $"Depunere reușită! Sold nou: {newBalance:F2} RON";
                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                        "DepositCompleted", "Deposit", "Form", "deposit-form",
                        null, amount.ToString("F2"), $"{{\"newBalance\":\"{newBalance:F2}\"}}");
                }
            }
            else
                TempData["DepositError"] = error ?? "Eroare la depunere.";

            return RedirectToAction("Deposit");
        }

        [HttpGet]
        public async Task<IActionResult> Withdraw()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                    "WithdrawStarted", "Withdraw", "Page", "WithdrawIndex",
                    null, null, "{}");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Withdraw(decimal amount, string iban)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            if (amount < 10)
            {
                TempData["WithdrawError"] = "Suma minimă de retragere este 10 RON.";
                return RedirectToAction("Withdraw");
            }

            if (string.IsNullOrWhiteSpace(iban) || iban.Replace(" ", "").Length < 15)
            {
                TempData["WithdrawError"] = "IBAN invalid.";
                return RedirectToAction("Withdraw");
            }

            TempData["WithdrawPending"] = amount.ToString("F2");
            return RedirectToAction("Withdraw");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var currentUser = await _apiService.GetCurrentUserAsync(token);
            var wallet = await _apiService.GetMyWalletAsync(token);
            var limits = await _apiService.GetMyLimitsAsync(token);

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                    "ProfileViewed", "Profile", "Page", "ProfileIndex",
                    null, null, "{}");
            }

            var model = new ProfilePageViewModel
            {
                User = currentUser,
                WalletInfo = wallet == null ? null : new DemoUserViewModel
                {
                    UserId = wallet.UserId,
                    Balance = wallet.Balance,
                    Currency = wallet.Currency
                },
                Limits = limits,
                SuccessMessage = TempData["Success"] as string,
                ErrorMessage = TempData["Error"] as string
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLimits(LimitsFormViewModel form)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var (success, _, error) = await _apiService.SetMyLimitsAsync(
                token,
                form.DailyDepositLimit,
                form.DailyLossLimit,
                form.DailySessionMinutesLimit,
                form.RealityCheckIntervalMinutes);

            if (success)
            {
                TempData["Success"] = "Limitele au fost actualizate cu succes.";
                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Ethical",
                        "ProfileEdited", "Profile", "Form", "limits-form",
                        null, null,
                        $"{{\"dailyDeposit\":\"{form.DailyDepositLimit}\",\"dailyLoss\":\"{form.DailyLossLimit}\"}}");
                }
            }
            else
                TempData["Error"] = error ?? "Eroare la actualizarea limitelor.";

            return RedirectToAction("Profile");
        }

        [HttpPost]
        public async Task<IActionResult> SelfExclude(string duration)
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var (success, _, error) = await _apiService.SelfExcludeAsync(token, duration);

            if (success)
                TempData["Success"] = "Auto-excluderea a fost activată. Te rugăm să folosești această perioadă pentru tine.";
            else
                TempData["Error"] = error ?? "Eroare la auto-excludere.";

            return RedirectToAction("Profile");
        }

        [HttpPost]
        public async Task<IActionResult> CancelSelfExclude()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var (success, _, error) = await _apiService.CancelSelfExcludeAsync(token);

            if (success)
                TempData["Success"] = "Auto-excluderea a fost anulată.";
            else
                TempData["Error"] = error ?? "Eroare la anulare.";

            return RedirectToAction("Profile");
        }
    }
}