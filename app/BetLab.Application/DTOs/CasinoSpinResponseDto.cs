using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class CasinoSpinResponseDto
    {
        public long CasinoRoundId { get; set; }
        public int CasinoGameId { get; set; }
        public decimal Stake { get; set; }
        public decimal Multiplier { get; set; }
        public decimal Payout { get; set; }
        public string ResultType { get; set; } = string.Empty;
        public string SymbolsJson { get; set; } = string.Empty;
        public decimal NewBalance { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
