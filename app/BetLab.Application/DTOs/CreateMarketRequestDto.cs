using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class CreateMarketRequestDto
    {
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public bool IsMain { get; set; } = false;
    }
}
