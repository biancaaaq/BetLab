using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class PlaceBetRequestDto
    {
        public Guid UserId { get; set; }
        public decimal Stake { get; set; }
        public List<PlaceBetSelectionRequestDto> Selections { get; set; } = new();
    }
}