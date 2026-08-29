namespace BetLab.DarkWeb.Models
{
    public class CasinoPageViewModel
    {
        public List<CasinoGameViewModel> Games { get; set; } = new();
        public CasinoGameViewModel? SelectedGame { get; set; }
        public CasinoSpinResponseViewModel? LastSpin { get; set; }
        public List<CasinoRoundHistoryViewModel> History { get; set; } = new();
    }
}