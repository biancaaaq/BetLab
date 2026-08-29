using BetLab.DarkWeb.Models;
using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiService _apiService;
        private readonly DarkSessionManager _sessionManager;

        public AccountController(ApiService apiService, DarkSessionManager sessionManager)
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
                ViewBag.Error = "Login eșuat.";
                return View(model);
            }

            HttpContext.Session.SetString("Dark_JwtToken", result.Token);
            HttpContext.Session.SetString("Dark_CurrentEmail", result.Email);
            HttpContext.Session.SetString("Dark_CurrentUserName", result.UserName);
            HttpContext.Session.SetString("Dark_CurrentUserId", result.UserId.ToString());

            if (result.Roles.Contains("Admin"))
                HttpContext.Session.SetString("Dark_IsAdmin", "true");

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
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
                ViewBag.Error = "Toate câmpurile sunt obligatorii.";
                return View(model);
            }

            var (result, _) = await _apiService.RegisterAsync(
                model.Email, model.UserName, model.Password, model.Cnp);

            if (result == null)
            {
                // Dark: mesaj vag — userul nu știe de ce a eșuat (dark pattern: opacity)
                ViewBag.Error = "Înregistrare eșuată. Încearcă din nou.";
                return View(model);
            }

            HttpContext.Session.SetString("Dark_JwtToken", result.Token);
            HttpContext.Session.SetString("Dark_CurrentEmail", result.Email);
            HttpContext.Session.SetString("Dark_CurrentUserName", result.UserName);
            HttpContext.Session.SetString("Dark_CurrentUserId", result.UserId.ToString());

            if (result.Roles.Contains("Admin"))
                HttpContext.Session.SetString("Dark_IsAdmin", "true");

            var regUserId = _sessionManager.GetCurrentUserId();
            var regSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (regUserId.HasValue && regSessionId.HasValue)
            {
                await _apiService.LogEventAsync(regSessionId.Value, regUserId.Value, "Dark",
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
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                    "Logout", "Home", "Button", "logout-button",
                    null, null, "{}");
                // Închidem sesiunea de experiment în DB (setează EndedAt)
                await _apiService.EndSessionAsync(experimentSessionId.Value, userId.Value);
            }

            _sessionManager.ClearExperimentSession();
            HttpContext.Session.Remove("Dark_JwtToken");
            HttpContext.Session.Remove("Dark_CurrentEmail");
            HttpContext.Session.Remove("Dark_CurrentUserName");
            HttpContext.Session.Remove("Dark_CurrentUserId");
            HttpContext.Session.Remove("Dark_IsAdmin");

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Deposit()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                    "DepositStarted", "Deposit", "Page", "DepositIndex",
                    null, null, "{}");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Deposit(decimal amount)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            if (amount <= 0)
            {
                TempData["DepositError"] = "Suma trebuie să fie mai mare de 0.";
                return RedirectToAction("Deposit");
            }

            var (success, newBalance, error) = await _apiService.DepositAsync(token, amount);
            if (success)
            {
                TempData["DepositSuccess"] = $"Depunere reușită! Sold nou: {newBalance:F2} RON";
                var userId = _sessionManager.GetCurrentUserId();
                var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
                if (userId.HasValue && experimentSessionId.HasValue)
                {
                    await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
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
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
                    "WithdrawStarted", "Withdraw", "Page", "WithdrawIndex",
                    null, null, "{}");
            }

            return View();
        }

        [HttpPost]
        public IActionResult Withdraw(decimal amount, string iban)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            if (amount < 100)
            {
                TempData["WithdrawError"] = "Suma minimă de retragere este 100 RON.";
                return RedirectToAction("Withdraw");
            }

            if (string.IsNullOrWhiteSpace(iban) || iban.Length < 15)
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
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            var currentUser = await _apiService.GetCurrentUserAsync(token);
            var wallet = await _apiService.GetMyWalletAsync(token);

            var userId = _sessionManager.GetCurrentUserId();
            var experimentSessionId = await _sessionManager.EnsureExperimentSessionAsync();
            if (userId.HasValue && experimentSessionId.HasValue)
            {
                await _apiService.LogEventAsync(experimentSessionId.Value, userId.Value, "Dark",
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
                }
            };

            return View(model);
        }

        // Auto-excludere — funcțională dar cu friction moderată (dark pattern: îngropat, FOMO,
        // confirmări multiple, deflectare). 
        [HttpPost]
        public async Task<IActionResult> SelfExclude(string duration, bool confirmFinal = false)
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Login");

            if (!confirmFinal)
            {
                TempData["SelfExcludeError"] = "Trebuie să bifezi confirmarea finală pentru a continua.";
                return RedirectToAction("Profile");
            }

            var ok = await _apiService.SelfExcludeAsync(token, duration);
            if (ok)
            {
                TempData["SelfExcludeSuccess"] =
                    "Cererea ta de auto-excludere a fost înregistrată. Vei fi deconectat în câteva secunde.";
                // Deconectare imediată — coerent cu auto-excluderea
                HttpContext.Session.Clear();
                return RedirectToAction("Index", "Home");
            }

            TempData["SelfExcludeError"] = "Eroare la procesare. Încearcă mai târziu sau contactează suportul.";
            return RedirectToAction("Profile");
        }
    }
}