namespace BetLab.EthicalWeb.Models
{
    public class BetSlipItemViewModel
    {
        public int OutcomeId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int MarketId { get; set; }
        public string MarketName { get; set; } = string.Empty;
        public string OutcomeName { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public string MarketType { get; set; } = string.Empty;
        public string OutcomeCode { get; set; } = string.Empty;
    }
}