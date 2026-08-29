using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Sport
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public ICollection<Competition> Competitions { get; set; } = new List<Competition>();
        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
