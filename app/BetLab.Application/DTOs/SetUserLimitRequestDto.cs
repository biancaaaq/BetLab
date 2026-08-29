namespace BetLab.Application.DTOs
{
   
    /// Request pentru modificarea limitelor.
    /// Toate câmpurile sunt opționale — păstrează valorile anterioare pentru cele lăsate null.
   
    public class SetUserLimitRequestDto
    {
        public decimal? DailyDepositLimit { get; set; }
        public decimal? DailyLossLimit { get; set; }
        public int? DailySessionMinutesLimit { get; set; }
        public int? RealityCheckIntervalMinutes { get; set; }
    }
}