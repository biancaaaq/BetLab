namespace BetLab.EthicalWeb.Models
{
    public class ProfilePageViewModel
    {
        public CurrentUserViewModel? User { get; set; }
        public DemoUserViewModel? WalletInfo { get; set; }
        public UserLimitViewModel? Limits { get; set; }

        public string? SuccessMessage { get; set; }
       
        public string? ErrorMessage { get; set; }
    }
}