namespace BetLab.DarkWeb.Models
{
    public class PlacedBetSelectionViewModel
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int MarketId { get; set; }
        public string MarketName { get; set; } = string.Empty;
        public int OutcomeId { get; set; }
        public string OutcomeName { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public bool EventIsLive { get; set; }
        public string EventStatus { get; set; } = string.Empty;
    }
}