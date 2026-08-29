using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class OutcomeDto
    {
        public int OutcomeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
