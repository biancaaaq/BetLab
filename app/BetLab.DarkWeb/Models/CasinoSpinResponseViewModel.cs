namespace BetLab.DarkWeb.Models
{
    public class CasinoSpinResponseViewModel
    {
        public long CasinoRoundId { get; set; }
        public int CasinoGameId { get; set; }
        public decimal Stake { get; set; }
        public decimal Multiplier { get; set; }
        public decimal Payout { get; set; }
        public string ResultType { get; set; } = string.Empty;
        public string SymbolsJson { get; set; } = string.Empty;
        public decimal NewBalance { get; set; }
        public bool IsWin => Payout > 0;
        public DateTime CreatedAt { get; set; }
    }
}
