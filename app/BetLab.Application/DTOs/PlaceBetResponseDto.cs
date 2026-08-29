using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class PlaceBetResponseDto
    {
        public int BetSlipId { get; set; }
        public Guid UserId { get; set; }
        public decimal Stake { get; set; }
        public decimal TotalOdds { get; set; }
        public decimal PotentialPayout { get; set; }
        public decimal NewBalance { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<PlacedBetSelectionDto> Selections { get; set; } = new();
    }
}
