using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class CasinoGameDto
    {
        public int CasinoGameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal RtpPercent { get; set; }
        public string Volatility { get; set; } = string.Empty;
    }
}
