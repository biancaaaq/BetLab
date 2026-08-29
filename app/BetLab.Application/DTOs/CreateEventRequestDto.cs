using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class CreateEventRequestDto
    {
        public int SportId { get; set; }
        public int CompetitionId { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public string Status { get; set; } = "Open";
        public bool IsLive { get; set; } = false;
    }
}