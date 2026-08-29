using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Market
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event? Event { get; set; }

        public string Name { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public bool IsMain { get; set; } = false;

        public ICollection<Outcome> Outcomes { get; set; } = new List<Outcome>();
    }
}
