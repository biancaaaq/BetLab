using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class CasinoRound
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }
        public AppUser? User { get; set; }

        public int CasinoGameId { get; set; }
        public CasinoGame? CasinoGame { get; set; }

        public Guid? CasinoSessionId { get; set; }
        public CasinoSession? CasinoSession { get; set; }

        public decimal Stake { get; set; }
        public decimal Multiplier { get; set; }
        public decimal Payout { get; set; }

        public string ResultType { get; set; } = string.Empty; // Loss / SmallWin / Win / BigWin
        public string SymbolsJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
