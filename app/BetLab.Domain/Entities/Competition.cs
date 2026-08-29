using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Competition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public int SportId { get; set; }
        public Sport? Sport { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
