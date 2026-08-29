namespace BetLab.DarkWeb.Models
{
    public class EventAnalyticsViewModel
    {
        public int    EventId      { get; set; }
        public string HomeTeamName { get; set; } = "";
        public string AwayTeamName { get; set; } = "";
        public bool   HasRealData  { get; set; }
        public string Message      { get; set; } = "";

        public H2HAggregateViewModel       H2H           { get; set; } = new();
        public List<H2HMatchInfoViewModel> RecentMatches { get; set; } = new();
        public List<FormItemViewModel>     HomeForm      { get; set; } = new();
        public List<FormItemViewModel>     AwayForm      { get; set; } = new();
    }

    public class H2HAggregateViewModel
    {
        public int NumMatches { get; set; }
        public int TotalGoals { get; set; }
        public int HomeWins   { get; set; }
        public int HomeDraws  { get; set; }
        public int HomeLosses { get; set; }
        public int AwayWins   { get; set; }
        public int AwayDraws  { get; set; }
        public int AwayLosses { get; set; }
    }

    public class H2HMatchInfoViewModel
    {
        public DateTime DateUtc     { get; set; }
        public string   HomeTeam    { get; set; } = "";
        public string   AwayTeam    { get; set; } = "";
        public string   Competition { get; set; } = "";
        public int      HomeScore   { get; set; }
        public int      AwayScore   { get; set; }
    }

    public class FormItemViewModel
    {
        public DateTime DateUtc      { get; set; }
        public string   Result       { get; set; } = "D";
        public string   Opponent     { get; set; } = "";
        public int      GoalsFor     { get; set; }
        public int      GoalsAgainst { get; set; }
        public bool     WasHome      { get; set; }
    }

    public class EventPopularityViewModel
    {
        public int TotalSelections { get; set; }
        public List<OutcomePopularityViewModel> Outcomes { get; set; } = new();
    }

    public class OutcomePopularityViewModel
    {
        public int    OutcomeId  { get; set; }
        public int    Count      { get; set; }
        public double Percentage { get; set; }
    }

    
    public class EventDetailsPageViewModel
    {
        public EventDetailsViewModel        Event       { get; set; } = new();
        public EventAnalyticsViewModel?     Analytics   { get; set; }
        public EventPopularityViewModel?    Popularity  { get; set; }
    }
}
