namespace BetLab.Domain.Entities
{
    public class RouletteRound
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        // Tipul pariului: RedBlack, OddEven, LowHigh, Dozen, Column, Straight
        public string BetType { get; set; } = string.Empty;

        public string BetValue { get; set; } = string.Empty;

        public decimal Stake { get; set; }
        public int ResultNumber { get; set; }
        public string ResultColor { get; set; } = string.Empty; // Red, Black, Green
        public bool IsWin { get; set; }
        public decimal Payout { get; set; }
        public decimal NewBalance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
