using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class InteractionLog
    {
        public long Id { get; set; }

        public Guid SessionId { get; set; }
        public ExperimentSession? Session { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public string Variant { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? MetadataJson { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
