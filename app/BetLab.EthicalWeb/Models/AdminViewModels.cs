namespace BetLab.EthicalWeb.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers        { get; set; }
        public int ActiveUsers       { get; set; }
        public int BlockedUsers      { get; set; }
        public int ActiveEvents      { get; set; }
        public int OpenBets          { get; set; }
        public int WonBets           { get; set; }
        public int LostBets          { get; set; }
        public int ActiveExclusions  { get; set; }
        public int TodayCasinoRounds { get; set; }
    }

    public class AdminUserViewModel
    {
        public Guid   UserId      { get; set; }
        public string Email       { get; set; } = string.Empty;
        public string UserName    { get; set; } = string.Empty;
        public bool   IsActive    { get; set; }
        public bool   IsExcluded  { get; set; }
        public decimal Balance    { get; set; }
        public string Currency    { get; set; } = "RON";
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class AdminEventViewModel
    {
        public int    Id          { get; set; }
        public string HomeTeam    { get; set; } = string.Empty;
        public string AwayTeam    { get; set; } = string.Empty;
        public string Competition { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public string Status      { get; set; } = string.Empty;
        public bool   IsLive      { get; set; }
        public int    BetCount    { get; set; }
        public List<AdminMarketViewModel> Markets { get; set; } = new();
    }

    public class AdminMarketViewModel
    {
        public int    Id         { get; set; }
        public string Name       { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Status     { get; set; } = string.Empty;
        public List<AdminOutcomeViewModel> Outcomes { get; set; } = new();
    }

    public class AdminOutcomeViewModel
    {
        public int    Id        { get; set; }
        public string Name      { get; set; } = string.Empty;
        public string Code      { get; set; } = string.Empty;
        public decimal Odds     { get; set; }
        public bool   IsWinning { get; set; }
        public string Status    { get; set; } = string.Empty;
    }

    public class AdminLookupItem
    {
        public int    Id   { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AdminAddEventViewModel
    {
        public List<AdminLookupItem> Sports       { get; set; } = new();
        public List<AdminLookupItem> Competitions { get; set; } = new();
        public List<AdminLookupItem> Teams        { get; set; } = new();
    }

    public class AdminCasinoGameViewModel
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = string.Empty;
        public string Type        { get; set; } = string.Empty;
        public decimal RtpPercent { get; set; }
        public string Volatility  { get; set; } = string.Empty;
        public bool   IsActive    { get; set; }
    }

    public class AdminExclusionViewModel
    {
        public long   Id        { get; set; }
        public Guid   UserId    { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName  { get; set; } = string.Empty;
        public DateTime ExcludedAt    { get; set; }
        public DateTime? ExcludedUntil { get; set; }
        public string Reason          { get; set; } = string.Empty;
        public string ImposedBy       { get; set; } = string.Empty;
        public bool   IsActive        { get; set; }
        public DateTime? LiftedAt     { get; set; }
        public string? LiftedByAdminEmail { get; set; }
    }

    
    public class AdminTransactionViewModel
    {
        public int      Id            { get; set; }
        public string   Type          { get; set; } = string.Empty;
        public decimal  Amount        { get; set; }
        public decimal  BalanceBefore { get; set; }
        public decimal  BalanceAfter  { get; set; }
        public DateTime CreatedAt     { get; set; }
        public string?  Description   { get; set; }
        public string?  ReferenceType { get; set; }
        public int?     ReferenceId   { get; set; }
        public Guid?    UserId        { get; set; }
        public string?  UserEmail     { get; set; }
        public string?  UserName      { get; set; }
    }

    public class AdminTransactionsPageViewModel
    {
        public List<AdminTransactionViewModel> Items { get; set; } = new();
        public int    Total    { get; set; }
        public int    Page     { get; set; }
        public int    PageSize { get; set; }
        public string? Filter  { get; set; }
    }

}
