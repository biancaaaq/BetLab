using BetLab.EthicalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.EthicalWeb.ViewComponents
{
    public class BalanceViewComponent : ViewComponent
    {
        private readonly ApiService _apiService;

        public BalanceViewComponent(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var token = HttpContext.Session.GetString("Ethical_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return Content(string.Empty);

            var wallet = await _apiService.GetMyWalletAsync(token);
            var balance = wallet?.Balance ?? 0m;

            return View(balance);
        }
    }
}
