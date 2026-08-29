using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class ExperimentSession
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public string Variant { get; set; } = string.Empty; // Ethical / Dark
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        public string? DeviceType { get; set; }
        public string? Browser { get; set; }

        public ICollection<InteractionLog> InteractionLogs { get; set; } = new List<InteractionLog>();
    }
}
