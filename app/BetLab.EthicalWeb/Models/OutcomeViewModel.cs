namespace BetLab.EthicalWeb.Models
{
    public class OutcomeViewModel
    {
        public int OutcomeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}