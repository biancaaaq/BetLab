using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Identity;

namespace BetLab.Domain.Entities
{
    public class AppUser : IdentityUser<Guid>
    {
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Wallet? Wallet { get; set; }
        public ICollection<BetSlip> BetSlips { get; set; } = new List<BetSlip>();
        public ICollection<ExperimentSession> ExperimentSessions { get; set; } = new List<ExperimentSession>();

        public ICollection<UserLimit> Limits { get; set; } = new List<UserLimit>();
        public string? Cnp { get; set; }
    }
}