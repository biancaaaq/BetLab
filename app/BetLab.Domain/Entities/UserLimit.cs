using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BetLab.Domain.Entities
{
    
    /// Limite de responsible gambling setate de utilizator pentru varianta Ethical.
    /// Fiecare modificare creează o entry nouă — limita activă este cea mai recentă.
    /// Conform Gainsbury et al. 2014, decrease se aplică imediat, increase necesită 24h cooldown.
    public class UserLimit
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public decimal? DailyDepositLimit { get; set; }

        public decimal? DailyLossLimit { get; set; }

        public int? DailySessionMinutesLimit { get; set; }

        public int RealityCheckIntervalMinutes { get; set; } = 30;

        public DateTime? SelfExcludedUntilUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}