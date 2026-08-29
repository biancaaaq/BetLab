namespace BetLab.EthicalWeb.Models
{
    /// Folosit la POST din formularul de limite.
    public class LimitsFormViewModel
    {
        public decimal? DailyDepositLimit { get; set; }
        public decimal? DailyLossLimit { get; set; }
        public int? DailySessionMinutesLimit { get; set; }
        public int? RealityCheckIntervalMinutes { get; set; }
    }
}