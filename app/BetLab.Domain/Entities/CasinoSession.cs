using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class CasinoSession
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public int CasinoGameId { get; set; }
        public CasinoGame? CasinoGame { get; set; }

        public string Variant { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        public ICollection<CasinoRound> Rounds { get; set; } = new List<CasinoRound>();
    }
}
