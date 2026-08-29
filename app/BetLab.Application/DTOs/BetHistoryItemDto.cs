using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class BetHistoryItemDto
    {
        public int BetSlipId { get; set; }
        public decimal Stake { get; set; }
        public decimal TotalOdds { get; set; }
        public decimal PotentialPayout { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime PlacedAt { get; set; }
        public decimal? ActualPayout { get; set; }
        public List<PlacedBetSelectionDto> Selections { get; set; } = new();
    }
}