namespace BetLab.DarkWeb.Models
{
    public class CasinoRoundHistoryViewModel
    {
        public long CasinoRoundId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public decimal Stake { get; set; }
        public decimal Multiplier { get; set; }
        public decimal Payout { get; set; }
        public string ResultType { get; set; } = string.Empty;
        public string SymbolsJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}