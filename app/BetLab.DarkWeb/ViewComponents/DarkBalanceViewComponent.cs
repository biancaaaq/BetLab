using BetLab.DarkWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BetLab.DarkWeb.ViewComponents
{
    public class DarkBalanceViewComponent : ViewComponent
    {
        private readonly ApiService _apiService;

        public DarkBalanceViewComponent(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var token = HttpContext.Session.GetString("Dark_JwtToken");
            if (string.IsNullOrWhiteSpace(token))
                return Content(string.Empty);

            var wallet = await _apiService.GetMyWalletAsync(token);
            var balance = wallet?.Balance ?? 0m;

            return View(balance);
        }
    }
}
