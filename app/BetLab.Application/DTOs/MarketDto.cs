using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class MarketDto
    {
        public int MarketId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public List<OutcomeDto> Outcomes { get; set; } = new();
    }
}
