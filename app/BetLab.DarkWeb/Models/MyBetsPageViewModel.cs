namespace BetLab.DarkWeb.Models
{
    public class MyBetsPageViewModel
    {
        public List<BetHistoryItemViewModel> Bets { get; set; } = new();
        public string ActiveTab { get; set; } = "all";

        /// Preview cash-out per bilet (calculat realist pe backend). Keyed by BetSlipId
        public Dictionary<int, CashOutPreviewViewModel> CashOutPreviews { get; set; } = new();

        public List<BetHistoryItemViewModel> Active =>
            Bets.Where(b => b.Status is "Pending" or "Active" or "Open").ToList();
        public List<BetHistoryItemViewModel> Won =>
            Bets.Where(b => b.Status == "Won").ToList();
        public List<BetHistoryItemViewModel> Lost =>
            Bets.Where(b => b.Status == "Lost").ToList();
        public List<BetHistoryItemViewModel> CashedOut =>
            Bets.Where(b => b.Status == "CashedOut").ToList();

        public List<BetHistoryItemViewModel> CurrentTab => ActiveTab switch
        {
            "active"    => Active,
            "won"       => Won,
            "lost"      => Lost,
            "cashedout" => CashedOut,
            _           => Bets
        };
    }
}
