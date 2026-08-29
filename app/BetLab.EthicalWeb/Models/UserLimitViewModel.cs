using System;

namespace BetLab.EthicalWeb.Models
{
    public class UserLimitViewModel
    {
        public Guid UserId { get; set; }
        public decimal? DailyDepositLimit { get; set; }
        public decimal? DailyLossLimit { get; set; }
        public int? DailySessionMinutesLimit { get; set; }
        public int RealityCheckIntervalMinutes { get; set; } = 30;
        public DateTime? SelfExcludedUntilUtc { get; set; }
        public bool IsPermanentlyExcluded { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsSelfExcludedNow =>
            IsPermanentlyExcluded ||
            (SelfExcludedUntilUtc.HasValue && SelfExcludedUntilUtc.Value > DateTime.UtcNow);
    }
}