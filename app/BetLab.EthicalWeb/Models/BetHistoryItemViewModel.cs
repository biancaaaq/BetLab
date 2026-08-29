namespace BetLab.EthicalWeb.Models
{
    public class BetHistoryItemViewModel
    {
        public int BetSlipId { get; set; }
        public decimal Stake { get; set; }
        public decimal TotalOdds { get; set; }
        public decimal PotentialPayout { get; set; }
        public decimal? ActualPayout { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime PlacedAt { get; set; }
        public List<PlacedBetSelectionViewModel> Selections { get; set; } = new();
    }
}