namespace BetLab.EthicalWeb.Models
{
    public class MyActivityPageViewModel
    {
        public WalletDtoViewModel? Wallet { get; set; }
        public List<BetHistoryItemViewModel> Bets { get; set; } = new();
        public List<WalletTransactionViewModel> Transactions { get; set; } = new();
    }
}