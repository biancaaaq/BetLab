namespace BetLab.EthicalWeb.Models
{
    public class BetSlipViewModel
    {
        public List<BetSlipItemViewModel> Items { get; set; } = new();
        public decimal Stake { get; set; }
        public decimal TotalOdds { get; set; }
        public decimal PotentialPayout { get; set; }
    }
}