namespace BetLab.EthicalWeb.Models
{
    public class CasinoPageViewModel
    {
        public List<CasinoGameViewModel> Games { get; set; } = new();
        public CasinoGameViewModel? SelectedGame { get; set; }
        public CasinoSpinResponseViewModel? LastSpin { get; set; }
        public List<CasinoRoundHistoryViewModel> History { get; set; } = new();
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "RON";
    }
}