namespace BetLab.DarkWeb.Models
{
    public class RouletteHistoryViewModel
    {
        public long RouletteRoundId { get; set; }
        public string BetType { get; set; } = string.Empty;
        public string BetValue { get; set; } = string.Empty;
        public decimal Stake { get; set; }
        public int ResultNumber { get; set; }
        public string ResultColor { get; set; } = string.Empty;
        public bool IsWin { get; set; }
        public decimal Payout { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
