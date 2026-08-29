namespace BetLab.Domain.Entities
{
    public class UserExclusion
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExcludedUntil { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string ImposedBy { get; set; } = "Admin";

        public bool IsActive { get; set; } = true;

        public DateTime? LiftedAt { get; set; }
        public string? LiftedByAdminEmail { get; set; }
    }
}
