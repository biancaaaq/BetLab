using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class BetSlip
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public decimal Stake { get; set; }
        public decimal TotalOdds { get; set; }
        public decimal PotentialPayout { get; set; }
        public decimal? ActualPayout { get; set; }

        public string Status { get; set; } = "Open";
        public string Type { get; set; } = "Single";

        public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }

        public ICollection<BetSelection> Selections { get; set; } = new List<BetSelection>();
    }
}
