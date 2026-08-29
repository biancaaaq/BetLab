using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetLab.Application.DTOs
{
    public class DemoUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}
