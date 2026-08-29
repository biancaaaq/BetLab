using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Outcome
    {
        public int Id { get; set; }

        public int MarketId { get; set; }
        public Market? Market { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Odds { get; set; }
        public string Status { get; set; } = "Open";
        public bool IsWinning { get; set; } = false;
    }
}
