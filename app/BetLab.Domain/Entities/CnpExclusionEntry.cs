namespace BetLab.Domain.Entities
{

    public class CnpExclusionEntry
    {
        public long Id { get; set; }

        public string Cnp { get; set; } = string.Empty;

        public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;

        public Guid? UserId { get; set; }

        public string? Reason { get; set; }
    }
}
