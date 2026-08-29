namespace BetLab.Domain.Entities
{
    public class BlackjackSession
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public string DeckJson { get; set; } = "[]";
        public string PlayerCardsJson { get; set; } = "[]";
        public string DealerCardsJson { get; set; } = "[]";

        public decimal Stake { get; set; }
        public string Status { get; set; } = "Active";
        public string? Result { get; set; }
        public decimal Payout { get; set; }
        public decimal NewBalance { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
