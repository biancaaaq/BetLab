using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class StartSessionResponseDto
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string Variant { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
    }
}
