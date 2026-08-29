namespace BetLab.DarkWeb.Models
{
    public class MarketViewModel
    {
        public int MarketId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public List<OutcomeViewModel> Outcomes { get; set; } = new();
    }
}