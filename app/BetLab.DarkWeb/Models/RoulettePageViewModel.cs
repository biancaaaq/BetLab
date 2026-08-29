namespace BetLab.DarkWeb.Models
{
    public class RoulettePageViewModel
    {
        public RouletteSpinResponseViewModel? LastSpin { get; set; }
        public List<RouletteHistoryViewModel> History { get; set; } = new();
    }
}
