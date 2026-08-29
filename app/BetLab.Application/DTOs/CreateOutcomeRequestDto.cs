using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class CreateOutcomeRequestDto
    {
        public int MarketId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public string Status { get; set; } = "Open";
    }
}
