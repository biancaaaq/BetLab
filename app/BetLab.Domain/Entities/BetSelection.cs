using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class BetSelection
    {
        public int Id { get; set; }

        public int BetSlipId { get; set; }
        public BetSlip? BetSlip { get; set; }

        public int EventId { get; set; }
        public Event? Event { get; set; }

        public int MarketId { get; set; }
        public Market? Market { get; set; }

        public int OutcomeId { get; set; }
        public Outcome? Outcome { get; set; }

        public decimal SelectedOdds { get; set; }
        public string PickName { get; set; } = string.Empty;
        public string ResultStatus { get; set; } = "Pending";
    }
}
