using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Domain.Entities
{
    public class Event
    {
        public int Id { get; set; }

        public int SportId { get; set; }
        public Sport? Sport { get; set; }

        public int CompetitionId { get; set; }
        public Competition? Competition { get; set; }

        public int HomeTeamId { get; set; }
        public Team? HomeTeam { get; set; }

        public int AwayTeamId { get; set; }
        public Team? AwayTeam { get; set; }

        public DateTime StartTimeUtc { get; set; }
        public string Status { get; set; } = "Scheduled";
        public bool IsLive { get; set; } = false;

        /// ID-ul meciului din football-data.org. Null pentru meciuri create manual
        public int? ExternalMatchId { get; set; }

        /// Scor curent — actualizat manual de admin sau prin sync.
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }

        /// Minutul curent al meciului, sincronizat din API-FOOTBALL (status.elapsed). Null = pre-meci sau necunoscut.
        public int? CurrentMinute { get; set; }

        public ICollection<Market> Markets { get; set; } = new List<Market>();
    }
}
