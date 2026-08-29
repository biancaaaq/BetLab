namespace BetLab.DarkWeb.Models
{
    public class EventListItemViewModel
    {
        public int EventId { get; set; }
        public string SportName { get; set; } = string.Empty;
        public string CompetitionName { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsLive { get; set; }
        public int MarketsCount { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}